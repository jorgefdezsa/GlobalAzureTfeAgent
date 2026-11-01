#!/usr/bin/env bash
# Prepara el despliegue continuo desde GitHub Actions: identidad de Azure sin secretos (OIDC) y variables del repositorio.
#
# Uso (desde la raíz, con `az login` y `gh auth login` hechos):
#   bash deploy/setup-github-cd.sh
#
# Crea (si no existe) un registro de aplicación con credencial federada para la rama main del repositorio,
# le da Contributor sobre el grupo de recursos y Azure AI User sobre la cuenta de Foundry, y define las
# variables AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID, ACR_NAME y AZD_ENV en GitHub.
# No hay secretos: las variables son identificadores y la clave del MCP no se copia a GitHub.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"

REPO="${REPO:-jorgefdezsa/GlobalAzureTfeAgent}"
RG="${RG:-GlobalAzureTenerife}"
APP_NAME="${APP_NAME:-gh-globalazuretfeagent-deploy}"
AZD_ENV_NAME="${AZD_ENV_NAME:-netcoreconf-dev}"
ENV_FILE=".azure/$AZD_ENV_NAME/.env"

[ -f "$ENV_FILE" ] || { echo "No existe $ENV_FILE: ejecuta el script desde la raíz del repositorio"; exit 1; }

TENANT="$(az account show --query tenantId -o tsv)"
SUB="$(az account show --query id -o tsv)"
RG_ID="/subscriptions/$SUB/resourceGroups/$RG"
ACR="$(az acr list -g "$RG" --query "[?starts_with(name,'acrglobaldemo')].name | [0]" -o tsv)"
ACCOUNT_ID="$(az cognitiveservices account list -g "$RG" --query "[0].id" -o tsv)"
echo "==> Repo $REPO | RG $RG | ACR $ACR | Foundry $ACCOUNT_ID"

echo "==> Registro de aplicación"
APP_ID="$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)"
if [ -z "$APP_ID" ]; then
  APP_ID="$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)"
  az ad sp create --id "$APP_ID" >/dev/null
fi

echo "==> Credencial federada (rama main)"
if [ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='github-main'].name | [0]" -o tsv)" ]; then
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"github-main\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$REPO:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }" >/dev/null
fi

echo "==> Roles"
SP_OID="$(az ad sp show --id "$APP_ID" --query id -o tsv)"
az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal \
  --role Contributor --scope "$RG_ID" >/dev/null 2>&1 || true
az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal \
  --role "Azure AI User" --scope "$ACCOUNT_ID" >/dev/null 2>&1 || true

echo "==> Variables de GitHub"
gh variable set AZURE_CLIENT_ID --repo "$REPO" --body "$APP_ID"
gh variable set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUB"
gh variable set ACR_NAME --repo "$REPO" --body "$ACR"
# El entorno de azd sin la clave del MCP (el workflow la lee de la Container App).
grep -v '^MCP_SUBSCRIPTION_KEY=' "$ENV_FILE" | gh variable set AZD_ENV --repo "$REPO"

echo
echo "Listo. Lanza una prueba:  gh workflow run deploy-mcp.yml --repo $REPO"
