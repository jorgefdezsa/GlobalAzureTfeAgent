#!/usr/bin/env bash
# Cierra el acceso directo al MCP: solo el AI Gateway puede llamarlo.
#
# Uso (desde la raíz del repositorio, con `az login` y el entorno de azd seleccionado):
#   bash deploy/lock-backend.sh
#
# 1. Guarda en API Management la clave de los clientes (mcp-gateway-key, la actual MCP_SUBSCRIPTION_KEY) y una clave de
#    backend nueva (mcp-backend-key), y crea el interruptor mcp-blocked si no existe.
# 2. Publica la imagen del MCP que exige x-backend-key y le da la clave de backend como secreto.
# 3. Aplica deploy/gateway-policy.xml al servidor MCP del gateway.
# El agente y la conexión de Foundry no cambian: siguen presentando la misma clave en x-mcp-key. Entre los pasos 2 y 3
# hay unos segundos en los que el MCP responde 401. Las claves no se imprimen.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
APIM="${APIM:-apim-globaltfe}"
APP="${APP:-mcp-aviation}"
SUB="$(az account show --query id -o tsv)"
ACR="$(az acr list -g "$RG" --query "[?starts_with(name,'acrglobaldemo')].name | [0]" -o tsv)"
GATEWAY_KEY="$(azd env get-value MCP_SUBSCRIPTION_KEY)"
BACKEND_KEY="$(openssl rand -hex 24)"
TAG="v$(date +%Y%m%d%H%M%S)"
BASE="https://management.azure.com/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ApiManagement/service/$APIM"
API="$(az rest --method get --url "$BASE/apis?api-version=2024-06-01-preview" --query "value[?properties.type=='mcp'].name | [0]" -o tsv 2>/dev/null)"
[ -n "$API" ] || { echo "No encuentro un servidor MCP en $APIM"; exit 1; }
echo "==> $APIM | servidor MCP $API | ACR $ACR"

echo "==> Named values en API Management"
az apim nv create -g "$RG" -n "$APIM" --named-value-id mcp-gateway-key --display-name mcp-gateway-key --value "$GATEWAY_KEY" --secret true >/dev/null 2>&1
az apim nv create -g "$RG" -n "$APIM" --named-value-id mcp-backend-key --display-name mcp-backend-key --value "$BACKEND_KEY" --secret true >/dev/null 2>&1
az apim nv show -g "$RG" -n "$APIM" --named-value-id mcp-blocked >/dev/null 2>&1 \
  || az apim nv create -g "$RG" -n "$APIM" --named-value-id mcp-blocked --display-name mcp-blocked --value false >/dev/null 2>&1

echo "==> Imagen del MCP y clave de backend"
(cd src && az acr build -r "$ACR" -t "mcp:$TAG" -f GlobalDemo.Mcp/Dockerfile .)
az containerapp secret set -n "$APP" -g "$RG" --secrets "mcp-key=$BACKEND_KEY" >/dev/null
az containerapp update -n "$APP" -g "$RG" --image "$ACR.azurecr.io/mcp:$TAG" >/dev/null

echo "==> Policy del gateway"
POLICY="$(mktemp)"
python3 - <<PY > "$POLICY"
import json
print(json.dumps({"properties": {"format": "rawxml", "value": open("deploy/gateway-policy.xml").read()}}))
PY
az rest --method put --url "$BASE/apis/$API/policies/policy?api-version=2024-06-01-preview" --body "@$POLICY" >/dev/null
rm -f "$POLICY"

echo "==> Comprobaciones (esperado: 200, 401, 401)"
GW="https://$APIM.azure-api.net/$API"
FQDN="$(az containerapp show -n "$APP" -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)"
req() { curl -s -m 30 -o /dev/null -w '%{http_code}' -X POST "$1" -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' "${@:2}" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'; echo; }
# La nueva revisión de la Container App y la policy tardan en aplicarse: se espera hasta 2 minutos a que el gateway acepte la clave.
for _ in $(seq 1 24); do
  [ "$(req "$GW" -H "x-mcp-key: $GATEWAY_KEY")" = "200" ] && break
  sleep 5
done
echo "gateway con la clave del cliente: $(req "$GW" -H "x-mcp-key: $GATEWAY_KEY")"
echo "gateway sin clave:                $(req "$GW")"
echo "backend directo con esa clave:    $(req "https://$FQDN/mcp" -H "x-mcp-key: $GATEWAY_KEY" -H "x-backend-key: $GATEWAY_KEY")"
