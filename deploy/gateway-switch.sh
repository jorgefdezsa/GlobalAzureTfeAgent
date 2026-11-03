#!/usr/bin/env bash
# Interruptores del AI Gateway para la demo.
#
#   bash deploy/gateway-switch.sh status                    # estado actual
#   bash deploy/gateway-switch.sh all on|off                # bloquear / desbloquear todo el servidor MCP
#   bash deploy/gateway-switch.sh tools buscar_vuelo        # bloquear solo esas tools (lista separada por comas)
#   bash deploy/gateway-switch.sh tools none                # desbloquear las tools
set -euo pipefail
export PATH="$PATH:/opt/homebrew/bin"
export PYTHONWARNINGS=ignore
RG="${RG:-GlobalAzureTenerife}"; APIM="${APIM:-apim-globaltfe}"

set_nv() { az apim nv update -g "$RG" -n "$APIM" --named-value-id "$1" --value "$2" >/dev/null 2>&1; }
get_nv() { az apim nv show -g "$RG" -n "$APIM" --named-value-id "$1" --query value -o tsv 2>/dev/null; }
status() { echo "mcp-blocked        = $(get_nv mcp-blocked)"; echo "mcp-blocked-tools  = $(get_nv mcp-blocked-tools)"; }

case "${1:-}" in
  status) status ;;
  all)
    case "${2:-}" in on) set_nv mcp-blocked true ;; off) set_nv mcp-blocked false ;; *) echo "uso: all on|off"; exit 1 ;; esac
    status ;;
  tools)
    [ -n "${2:-}" ] || { echo "uso: tools <lista|none>"; exit 1; }
    set_nv mcp-blocked-tools "$2"; status ;;
  *) sed -n '2,8p' "$0"; exit 1 ;;
esac
