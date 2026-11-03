#!/usr/bin/env bash
# Arranca el panel de control de la demo en http://127.0.0.1:5200 (chat con el agente, trazas y bloqueos del AI Gateway).
#
# Uso (desde la raíz del repositorio, con `az login` hecho y el entorno de azd seleccionado):
#   bash deploy/run-control.sh
#
# Toma la configuración de azd y de Azure; ninguna clave llega al navegador. El backend usa tu identidad de az login para
# hablar con el agente y con API Management, así que solo escucha en localhost.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
APP="${APP:-mcp-aviation}"
WEB=src/GlobalDemo.Control

export Control__SubscriptionId="$(az account show --query id -o tsv)"
export Control__ResourceGroup="$RG"
export Control__Apim="${APIM:-apim-globaltfe}"
export Control__AgentEndpoint="$(azd env get-value AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT)"
export Control__McpGatewayUrl="$(azd env get-value MCP_URL)"
export Control__McpKey="$(azd env get-value MCP_SUBSCRIPTION_KEY)"
export Control__McpDirectUrl="https://$(az containerapp show -n "$APP" -g "$RG" --query properties.configuration.ingress.fqdn -o tsv)/mcp"

if [ ! -f "$WEB/wwwroot/index.html" ]; then
  echo "==> Compilando el frontend"
  (cd "$WEB/web" && npm install && npm run build)
fi

echo "==> Panel en http://127.0.0.1:5200"
dotnet run --project "$WEB" -c Release
