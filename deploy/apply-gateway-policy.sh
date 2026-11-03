#!/usr/bin/env bash
# Aplica deploy/gateway-policy.xml al servidor MCP del AI Gateway y crea los named values mcp-blocked-tools y mcp-rate-limit si faltan.
# No toca las claves ni la Container App (para eso, deploy/lock-backend.sh).
#
# Uso (desde la raíz del repositorio, con `az login` hecho):
#   bash deploy/apply-gateway-policy.sh
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
APIM="${APIM:-apim-globaltfe}"
SUB="$(az account show --query id -o tsv)"
BASE="https://management.azure.com/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ApiManagement/service/$APIM"
API="$(az rest --method get --url "$BASE/apis?api-version=2024-06-01-preview" --query "value[?properties.type=='mcp'].name | [0]" -o tsv 2>/dev/null)"
[ -n "$API" ] || { echo "No encuentro un servidor MCP en $APIM"; exit 1; }

for nv in mcp-blocked:false mcp-blocked-tools:none mcp-rate-limit:0; do
  id="${nv%%:*}"; default="${nv#*:}"
  az apim nv show -g "$RG" -n "$APIM" --named-value-id "$id" >/dev/null 2>&1 \
    || az apim nv create -g "$RG" -n "$APIM" --named-value-id "$id" --display-name "$id" --value "$default" >/dev/null 2>&1
done

POLICY="$(mktemp)"
python3 - <<PY > "$POLICY"
import json
print(json.dumps({"properties": {"format": "rawxml", "value": open("deploy/gateway-policy.xml").read()}}))
PY
az rest --method put --url "$BASE/apis/$API/policies/policy?api-version=2024-06-01-preview" --body "@$POLICY" >/dev/null
rm -f "$POLICY"
echo "Policy aplicada en $APIM / $API"
