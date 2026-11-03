#!/usr/bin/env bash
# Prepara la auditoría del AI Gateway: Application Insights, los named values de destino (sobre el Log Analytics del grupo de recursos),
# el logger y el diagnóstico de API Management, y una clave propia para el panel (mcp-key-panel).
# La clave del agente sigue siendo mcp-gateway-key; el panel se identifica con la suya.
# Es idempotente: se puede repetir. No toca la policy (para eso, deploy/apply-gateway-policy.sh).
#
# Uso (desde la raíz del repositorio, con `az login` y `azd env select` hechos):
#   bash deploy/setup-audit.sh
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
APIM="${APIM:-apim-globaltfe}"
INSIGHTS="${INSIGHTS:-appi-globaltfe}"
SUB="$(az account show --query id -o tsv)"
ARM="https://management.azure.com/subscriptions/$SUB/resourceGroups/$RG/providers"
V="api-version=2024-06-01-preview"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

echo "Application Insights ($INSIGHTS)…"
WS="$(az resource list -g "$RG" --resource-type Microsoft.OperationalInsights/workspaces --query "[0].id" -o tsv 2>/dev/null)"
LOC="$(az group show -n "$RG" --query location -o tsv 2>/dev/null)"
[ -n "$WS" ] || { echo "No encuentro un workspace de Log Analytics en $RG"; exit 1; }
printf '{"location":"%s","kind":"web","properties":{"Application_Type":"web","WorkspaceResourceId":"%s","IngestionMode":"LogAnalytics"}}' "$LOC" "$WS" > "$TMP/ai.json"
az rest --method put --url "$ARM/Microsoft.Insights/components/$INSIGHTS?api-version=2020-02-02" --body "@$TMP/ai.json" >/dev/null 2>&1
IKEY="$(az rest --method get --url "$ARM/Microsoft.Insights/components/$INSIGHTS?api-version=2020-02-02" --query properties.InstrumentationKey -o tsv 2>/dev/null)"
AIID="$(az resource show -g "$RG" -n "$INSIGHTS" --resource-type Microsoft.Insights/components --query id -o tsv 2>/dev/null)"

echo "Logger y diagnóstico en $APIM…"
printf '{"properties":{"loggerType":"applicationInsights","description":"Auditoría MCP","credentials":{"instrumentationKey":"%s"},"resourceId":"%s"}}' "$IKEY" "$AIID" > "$TMP/logger.json"
az rest --method put --url "$ARM/Microsoft.ApiManagement/service/$APIM/loggers/appinsights?$V" --body "@$TMP/logger.json" >/dev/null 2>&1
LOGGER="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.ApiManagement/service/$APIM/loggers/appinsights"
printf '{"properties":{"loggerId":"%s","alwaysLog":"allErrors","sampling":{"samplingType":"fixed","percentage":100},"verbosity":"information","logClientIp":false}}' "$LOGGER" > "$TMP/diag.json"
az rest --method put --url "$ARM/Microsoft.ApiManagement/service/$APIM/diagnostics/applicationinsights?$V" --body "@$TMP/diag.json" >/dev/null 2>&1

echo "Destino de la auditoría (named values)…"
ENDPOINT="$(az rest --method get --url "$ARM/Microsoft.Insights/components/$INSIGHTS?api-version=2020-02-02" --query properties.ConnectionString -o tsv 2>/dev/null | tr ';' '\n' | sed -n 's/^IngestionEndpoint=//p')"
for nv in "mcp-audit-endpoint:$ENDPOINT" "mcp-audit-ikey:$IKEY"; do
  id="${nv%%:*}"; value="${nv#*:}"
  az apim nv show -g "$RG" -n "$APIM" --named-value-id "$id" >/dev/null 2>&1 \
    && az apim nv update -g "$RG" -n "$APIM" --named-value-id "$id" --value "$value" >/dev/null 2>&1 \
    || az apim nv create -g "$RG" -n "$APIM" --named-value-id "$id" --display-name "$id" --value "$value" >/dev/null 2>&1
done

echo "Clave del panel (mcp-key-panel)…"
if ! az apim nv show -g "$RG" -n "$APIM" --named-value-id mcp-key-panel >/dev/null 2>&1; then
  KEY="$(python3 -c 'import secrets;print(secrets.token_urlsafe(32))')"
  az apim nv create -g "$RG" -n "$APIM" --named-value-id mcp-key-panel --display-name mcp-key-panel --value "$KEY" --secret true >/dev/null 2>&1
  azd env set MCP_PANEL_KEY "$KEY" >/dev/null
  echo "  creada y guardada en el entorno de azd como MCP_PANEL_KEY"
else
  echo "  ya existe"
fi

APPID="$(az rest --method get --url "$ARM/Microsoft.Insights/components/$INSIGHTS?api-version=2020-02-02" --query properties.AppId -o tsv 2>/dev/null)"
azd env set AUDIT_APPINSIGHTS_APP_ID "$APPID" >/dev/null
azd env set AUDIT_APPINSIGHTS_NAME "$INSIGHTS" >/dev/null
echo "Listo. Ahora: bash deploy/apply-gateway-policy.sh"
