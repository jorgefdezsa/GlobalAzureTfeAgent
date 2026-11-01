#!/usr/bin/env bash
# Publica el servidor MCP en Azure Container Apps y deja MCP_URL / MCP_SUBSCRIPTION_KEY en el entorno de azd.
#
# Uso (desde la raíz del repositorio, con `az login` en el tenant del recurso):
#   AVIATIONSTACK_KEY=<clave> bash deploy/deploy-mcp.sh
#
# Crea (si no existen) en el grupo GlobalAzureTenerife: un ACR Basic, un entorno de Container Apps y la app `mcp-aviation`.
# Cada ejecución genera una clave nueva para /mcp (rota la anterior) y la guarda en azd; no se imprime.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"

: "${AVIATIONSTACK_KEY:?Define AVIATIONSTACK_KEY con la clave de AviationStack (no la pegues en el repositorio)}"

RG="${RG:-GlobalAzureTenerife}"
LOC="${LOC:-$(az group show -n "$RG" --query location -o tsv)}"
CAE="${CAE:-cae-globaldemo}"
APP="${APP:-mcp-aviation}"
SUB_ID="$(az account show --query id -o tsv)"
ACR="${ACR:-acrglobaldemo$(printf %s "$SUB_ID" | shasum | cut -c1-8)}"   # nombre único y estable por suscripción
TAG="v$(date +%Y%m%d%H%M%S)"

echo "==> Suscripción $SUB_ID | grupo $RG ($LOC) | ACR $ACR | app $APP"

az extension add -n containerapp --upgrade -y >/dev/null
az provider register --namespace Microsoft.App --wait
az provider register --namespace Microsoft.OperationalInsights --wait

echo "==> Registro de contenedores"
az acr show -n "$ACR" -g "$RG" >/dev/null 2>&1 || az acr create -n "$ACR" -g "$RG" -l "$LOC" --sku Basic >/dev/null
ACR_ID="$(az acr show -n "$ACR" -g "$RG" --query id -o tsv)"

echo "==> Build de la imagen en Azure ($TAG)"
# Desde src: `az acr build` resuelve -f respecto al directorio actual, y el contexto es src.
(cd src && az acr build -r "$ACR" -t "mcp:$TAG" -f GlobalDemo.Mcp/Dockerfile .)

echo "==> Entorno de Container Apps"
az containerapp env show -n "$CAE" -g "$RG" >/dev/null 2>&1 || az containerapp env create -n "$CAE" -g "$RG" -l "$LOC" >/dev/null

# `env create` puede devolver antes de terminar: esperar a que esté aprovisionado antes de crear la app.
for i in $(seq 1 60); do
  STATE="$(az containerapp env show -n "$CAE" -g "$RG" --query properties.provisioningState -o tsv 2>/dev/null || true)"
  [ "$STATE" = "Succeeded" ] && break
  [ "$STATE" = "Failed" ] && { echo "El entorno $CAE falló al aprovisionarse"; exit 1; }
  echo "    entorno en estado '${STATE:-desconocido}', esperando... ($i/60)"
  sleep 10
done
[ "$STATE" = "Succeeded" ] || { echo "Tiempo agotado esperando al entorno $CAE"; exit 1; }

MCP_KEY="$(openssl rand -hex 24)"

if ! az containerapp show -n "$APP" -g "$RG" >/dev/null 2>&1; then
  echo "==> Creando la app (imagen provisional para obtener su identidad)"
  az containerapp create -n "$APP" -g "$RG" --environment "$CAE" \
    --image mcr.microsoft.com/k8se/quickstart:latest --target-port 80 --ingress external \
    --system-assigned --min-replicas 1 --max-replicas 1 >/dev/null
fi

echo "==> Permiso AcrPull para la identidad de la app"
PRINCIPAL="$(az containerapp show -n "$APP" -g "$RG" --query identity.principalId -o tsv)"
az role assignment create --assignee-object-id "$PRINCIPAL" --assignee-principal-type ServicePrincipal \
  --role AcrPull --scope "$ACR_ID" >/dev/null 2>&1 || true
az containerapp registry set -n "$APP" -g "$RG" --server "$ACR.azurecr.io" --identity system >/dev/null

echo "==> Secretos y despliegue de la imagen"
az containerapp secret set -n "$APP" -g "$RG" --secrets "aviation-key=$AVIATIONSTACK_KEY" "mcp-key=$MCP_KEY" >/dev/null
az containerapp ingress update -n "$APP" -g "$RG" --target-port 8080 >/dev/null
az containerapp update -n "$APP" -g "$RG" --image "$ACR.azurecr.io/mcp:$TAG" \
  --set-env-vars "AviationStack__AccessKey=secretref:aviation-key" "Mcp__ApiKey=secretref:mcp-key" >/dev/null

FQDN="$(az containerapp show -n "$APP" -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)"
echo "==> MCP publicado en https://$FQDN/mcp"

azd env set MCP_URL "https://$FQDN/mcp" >/dev/null
azd env set MCP_SUBSCRIPTION_KEY "$MCP_KEY" >/dev/null
echo "==> Guardados MCP_URL y MCP_SUBSCRIPTION_KEY en el entorno de azd"
echo
echo "Prueba (sin clave debe dar 401):"
echo "  curl -s -o /dev/null -w '%{http_code}\n' -X POST https://$FQDN/mcp"
