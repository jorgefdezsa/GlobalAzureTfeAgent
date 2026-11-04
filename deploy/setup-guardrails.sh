#!/usr/bin/env bash
# Crea o actualiza la RAI policy del hosted agent: guardrail de contenido (jailbreak, etc.) y control de egress
# (el agente solo puede salir hacia el AI Gateway y lo que la plataforma necesita). Las dos van en una sola policy,
# porque un agente solo admite una. Ambas funciones son preview (API 2026-05-15-preview).
#
# Uso (desde la raíz del repositorio, con `az login` y `azd env select` hechos):
#   bash deploy/setup-guardrails.sh audit      # el egress solo registra lo que bloquearía (para ensayar)
#   bash deploy/setup-guardrails.sh enforced   # el egress bloquea de verdad
#   bash deploy/setup-guardrails.sh status     # muestra la policy y la versión del agente que la usa
#
# Después de cambiar la policy hay que desplegar una versión nueva del agente (`azd deploy`) y abrir una
# sesión nueva: los sandboxes en marcha no recargan la policy. Es idempotente.
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore

RG="${RG:-GlobalAzureTenerife}"
ACCOUNT="${ACCOUNT:-globalazureagent}"
POLICY="${POLICY:-demo1-guardrail}"
MODE="${1:-audit}"
SUB="$(az account show --query id -o tsv)"
POLICY_ID="/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft.CognitiveServices/accounts/$ACCOUNT/raiPolicies/$POLICY"
URL="https://management.azure.com$POLICY_ID?api-version=2026-05-15-preview"

# Host del AI Gateway: el de la URL del MCP. Es el único destino que el agente debe poder alcanzar a propósito.
GATEWAY_HOST="${GATEWAY_HOST:-$(azd env get-value MCP_URL 2>/dev/null | sed -E 's#^https?://([^/]+).*#\1#')}"
[ -n "$GATEWAY_HOST" ] || { echo "No encuentro el host del gateway (MCP_URL en azd env)."; exit 1; }

case "$MODE" in
  status)
    az rest --method get --url "$URL" --query "{nombre:name, modo:properties.mode, base:properties.basePolicyName, egress:properties.egressPolicy}" -o jsonc 2>&1 || echo "La policy $POLICY no existe."
    exit 0 ;;
  audit) EGRESS_MODE="Audit" ;;
  enforced) EGRESS_MODE="Enforced" ;;
  *) echo "Uso: $0 [audit|enforced|status]"; exit 1 ;;
esac

# AviationStack NO está en la lista a propósito: el agente solo llega a los vuelos a través del gateway.
BODY="$(python3 - "$EGRESS_MODE" "$GATEWAY_HOST" <<'EOF'
import json, sys
mode, host = sys.argv[1], sys.argv[2]
# La API exige los filtros explícitos: son los de Microsoft.DefaultV2. El que importa en la demo es Jailbreak (prompt shields).
filters = [{"action": "NONE", "blocking": True, "enabled": True, "name": n, "severityThreshold": "Medium", "source": s}
           for n in ("Hate", "Sexual", "Violence", "Selfharm") for s in ("Prompt", "Completion")]
filters += [
    {"action": "NONE", "blocking": True, "enabled": True, "name": "Jailbreak", "source": "Prompt"},
    {"action": "NONE", "blocking": True, "enabled": True, "name": "Protected Material Text", "source": "Completion"},
    {"action": "NONE", "blocking": False, "enabled": True, "name": "Protected Material Code", "source": "Completion"},
]
print(json.dumps({"properties": {
    "mode": "Blocking",
    "basePolicyName": "Microsoft.DefaultV2",
    "contentFilters": filters,
    "egressPolicy": {
        "mode": mode,
        "defaultAction": "Deny",
        "rules": [
            {"name": "allow-ai-gateway", "ruleType": "Fqdn", "match": {"host": host}, "action": {"actionType": "Allow"}},
            # Telemetría del propio agente (Application Insights y Live Metrics): con Deny solo ensuciaba la traza con denegaciones.
            {"name": "allow-appinsights", "ruleType": "Fqdn", "match": {"host": "*.applicationinsights.azure.com"}, "action": {"actionType": "Allow"}},
            {"name": "allow-live-metrics", "ruleType": "Fqdn", "match": {"host": "*.livediagnostics.monitor.azure.com"}, "action": {"actionType": "Allow"}},
        ],
    },
}}))
EOF
)"

echo "RAI policy $POLICY en $ACCOUNT (egress $EGRESS_MODE, gateway $GATEWAY_HOST)…"
az rest --method put --url "$URL" --body "$BODY" --query "{nombre:name, egress:properties.egressPolicy.mode, accion:properties.egressPolicy.defaultAction}" -o jsonc
azd env set RAI_POLICY_ID "$POLICY_ID" >/dev/null
echo "Guardada como RAI_POLICY_ID en el entorno de azd."
echo "Ahora: azd deploy   (versión nueva del agente) y, en el panel, Nueva conversación."
