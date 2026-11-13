#!/usr/bin/env bash
# Publica el backend del panel (GlobalDemo.Control) en Azure Container Apps para que lo use la Canvas App.
#
# Uso (desde la raíz del repositorio, con `az login` y el entorno de azd seleccionado):
#   bash deploy/deploy-control.sh
#
# Crea una Container App NUEVA (`control-panel`) en el entorno que ya existe. No toca mcp-aviation, API Management, el agente
# ni la RAI policy, y no cambia el panel local (deploy/run-control.sh sigue funcionando igual, con tu az login).
#
# - Usa la identidad administrada de la app en lugar de `az login` (DefaultAzureCredential la recoge sola).
# - /api exige la cabecera x-panel-key. La clave se genera la primera vez, se guarda en el entorno de azd (CONTROL_PANEL_KEY)
#   y se reutiliza en las siguientes ejecuciones; no se imprime. Es la que irá en el conector personalizado.
# - Las claves viven como secretos de la Container App. Nada llega al repositorio ni a la Canvas App.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
APIM="${APIM:-apim-globaltfe}"
CAE="${CAE:-cae-globaldemo}"
APP="${APP:-control-panel}"
FOUNDRY="${FOUNDRY:-globalazureagent}"
SUB_ID="$(az account show --query id -o tsv)"
ACR="$(az acr list -g "$RG" --query "[?starts_with(name,'acrglobaldemo')].name | [0]" -o tsv)"
[ -n "$ACR" ] || { echo "No encuentro el registro de contenedores en $RG (¿has ejecutado deploy/deploy-mcp.sh?)"; exit 1; }
TAG="v$(date +%Y%m%d%H%M%S)"

echo "==> Suscripción $SUB_ID | grupo $RG | ACR $ACR | app $APP"
az extension add -n containerapp --upgrade -y >/dev/null

echo "==> Configuración (de azd y Azure)"
# `azd env get-value` escribe su error en la salida estándar cuando la clave no existe, así que un valor opcional no se puede leer con
# `2>/dev/null || true`: devolvería el texto del error. `get-values` solo lista lo que existe (CLAVE="valor").
azd_opt() { azd env get-values 2>/dev/null | sed -n "s/^$1=\"\(.*\)\"\$/\1/p" | head -n1; }
AGENT_ENDPOINT="$(azd env get-value AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT)"
MCP_URL="$(azd env get-value MCP_URL)"
MCP_KEY="$(azd_opt MCP_PANEL_KEY)"
[ -n "$MCP_KEY" ] || MCP_KEY="$(azd env get-value MCP_SUBSCRIPTION_KEY)"
APPINSIGHTS="$(azd_opt AUDIT_APPINSIGHTS_NAME)"
MCP_APP="${MCP_APP:-mcp-aviation}"
MCP_DIRECT="https://$(az containerapp show -n "$MCP_APP" -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)/mcp"
PANEL_KEY="$(azd_opt CONTROL_PANEL_KEY)"
# Una clave guardada por una versión anterior de este script podía ser un mensaje de error: solo se acepta una clave hexadecimal de 48 caracteres.
[[ "$PANEL_KEY" =~ ^[0-9a-f]{48}$ ]] || PANEL_KEY=""
if [ -z "$PANEL_KEY" ]; then
  PANEL_KEY="$(openssl rand -hex 24)"
  azd env set CONTROL_PANEL_KEY "$PANEL_KEY" >/dev/null
  echo "    clave del panel generada y guardada en azd (CONTROL_PANEL_KEY)"
fi

echo "==> Build de la imagen en Azure ($TAG)"
(cd src && az acr build -r "$ACR" -t "control:$TAG" -f GlobalDemo.Control/Dockerfile .)
ACR_ID="$(az acr show -n "$ACR" -g "$RG" --query id -o tsv)"

if ! az containerapp show -n "$APP" -g "$RG" >/dev/null 2>&1; then
  echo "==> Creando la app (imagen provisional para obtener su identidad)"
  # Una sola réplica: el registro de peticiones del panel vive en memoria.
  az containerapp create -n "$APP" -g "$RG" --environment "$CAE" \
    --image mcr.microsoft.com/k8se/quickstart:latest --target-port 80 --ingress external \
    --system-assigned --min-replicas 1 --max-replicas 1 >/dev/null
fi

echo "==> Permiso AcrPull para la identidad de la app"
PRINCIPAL="$(az containerapp show -n "$APP" -g "$RG" --query identity.principalId -o tsv)"
az role assignment create --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
  --role AcrPull --scope "$ACR_ID" >/dev/null 2>&1 || true
az containerapp registry set -n "$APP" -g "$RG" --server "$ACR.azurecr.io" --identity system >/dev/null

echo "==> Roles de la identidad para lo que hace el panel"
# Mínimos por recurso. Si algo falla con 403 al usar el panel, revisa aquí primero.
assign() { # rol, ámbito, descripción
  if az role assignment create --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
       --role "$1" --scope "$2" >/dev/null 2>&1; then echo "    ok   $1 · $3"
  else echo "    AVISO no se pudo asignar '$1' ($3): hazlo a mano o pide permisos de propietario"; fi
}
APIM_ID="$(az apim show -n "$APIM" -g "$RG" --query id -o tsv)"
FOUNDRY_ID="$(az cognitiveservices account show -n "$FOUNDRY" -g "$RG" --query id -o tsv)"
assign "API Management Service Contributor" "$APIM_ID" "cambiar los named values (bloqueos y límite)"
assign "Reader" "$FOUNDRY_ID" "leer la RAI policy (guardrail y egress)"
# «Azure AI User» pasó a llamarse «Foundry User»; ambos dan Microsoft.CognitiveServices/* en el plano de datos (invocar al agente).
# Tras asignarlo tarda unos minutos en propagarse: hasta entonces el chat da 403 (agents/write).
az role definition list --name "Foundry User" --query "[0].roleName" -o tsv 2>/dev/null | grep -q . && AGENT_ROLE="Foundry User" || AGENT_ROLE="Azure AI User"
assign "$AGENT_ROLE" "$FOUNDRY_ID" "invocar al agente"
if [ -n "$APPINSIGHTS" ]; then
  AI_ID="$(az resource show -g "$RG" -n "$APPINSIGHTS" --resource-type Microsoft.Insights/components --query id -o tsv)"
  assign "Monitoring Reader" "$AI_ID" "consultar la auditoría"
fi

echo "==> Secretos y despliegue de la imagen"
az containerapp secret set -n "$APP" -g "$RG" --secrets "panel-key=$PANEL_KEY" "mcp-key=$MCP_KEY" >/dev/null
az containerapp ingress update -n "$APP" -g "$RG" --target-port 8080 >/dev/null
az containerapp update -n "$APP" -g "$RG" --image "$ACR.azurecr.io/control:$TAG" --set-env-vars \
  "ASPNETCORE_URLS=http://0.0.0.0:8080" \
  "Control__SubscriptionId=$SUB_ID" "Control__ResourceGroup=$RG" "Control__Apim=$APIM" \
  "Control__FoundryAccount=$FOUNDRY" \
  "Control__AgentEndpoint=$AGENT_ENDPOINT" "Control__McpGatewayUrl=$MCP_URL" "Control__McpDirectUrl=$MCP_DIRECT" \
  "Control__AppInsights=$APPINSIGHTS" \
  "Control__McpKey=secretref:mcp-key" "Control__PanelKey=secretref:panel-key" \
  "Control__PriceInputPerMillion=$(azd_opt MODEL_PRICE_INPUT | grep -E '^[0-9.]+$' || echo 0)" \
  "Control__PriceCachedPerMillion=$(azd_opt MODEL_PRICE_CACHED | grep -E '^[0-9.]+$' || echo 0)" \
  "Control__PriceOutputPerMillion=$(azd_opt MODEL_PRICE_OUTPUT | grep -E '^[0-9.]+$' || echo 0)" >/dev/null

FQDN="$(az containerapp show -n "$APP" -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)"
azd env set CONTROL_PANEL_URL "https://$FQDN" >/dev/null
echo "==> Panel publicado en https://$FQDN (URL guardada en azd como CONTROL_PANEL_URL)"
echo
echo "Pruebas:"
echo "  sin clave, debe dar 401:  curl -s -o /dev/null -w '%{http_code}\n' https://$FQDN/api/state"
echo "  con clave, debe dar 200:  curl -s -o /dev/null -w '%{http_code}\n' -H \"x-panel-key: \$(azd env get-value CONTROL_PANEL_KEY)\" https://$FQDN/api/state"
echo
echo "Conector de la Canvas App: host $FQDN, autenticación por clave de API, cabecera x-panel-key."
