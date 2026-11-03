# globalDemo

Demo para NetCoreConf: un **asistente de vuelos** construido con .NET 10, Aspire y Microsoft Agent Framework.
El agente consulta vuelos reales (AviationStack) a través de un servidor **MCP** y usa un modelo desplegado en **Microsoft Foundry**.
Puede ejecutarse en local con Aspire o publicarse como **Foundry Hosted Agent**.

```
Usuario ──► Agente (Agent Framework) ──► MCP (6 tools) ──► GlobalDemo.Aviation ──► AviationStack
                 │
                 └──► Modelo gpt-6.1-terra en Foundry (Responses API)
```

## Estructura

| Proyecto | Qué hace |
|---|---|
| `src/GlobalDemo.Aviation` | Librería compartida: cliente tipado de AviationStack, modelos, caché (HybridCache, 2 min) y resiliencia. |
| `src/GlobalDemo.Api` | API REST sobre la librería (`/api/flights`, `/api/airports`, `/api/airlines`…). |
| `src/GlobalDemo.Mcp` | Servidor MCP (Streamable HTTP en `/mcp`) con las 6 tools de vuelos. |
| `src/GlobalDemo.Agent` | Agente local con endpoint `/chat`; consume el MCP. Es el que corre con Aspire. |
| `src/GlobalDemo.AgentHosted` | El mismo agente empaquetado como Foundry Hosted Agent (protocolo Responses, puerto 8088). |
| `src/GlobalDemo.Control` | Panel web de la demo (backend .NET + frontend React): chat con el agente, traza de cada llamada y bloqueos del AI Gateway. |
| `src/GlobalDemo.DemoClient` | Consola de la demo: llama al agente hospedado con streaming y pinta tool calls, resultados y bloqueos en vivo. |
| `src/GlobalDemo.AppHost` | Orquestación Aspire (api, mcp, agent). |
| `src/GlobalDemo.ServiceDefaults` | Telemetría, health checks y service discovery comunes. |
| `deploy/deploy-mcp.sh` | Publica el MCP en Azure Container Apps. |
| `azure.yaml` | Definición de `azd` para el agente hospedado. |

## Tools del MCP

| Tool | Descripción |
|---|---|
| `buscar_vuelo` | Estado, horarios, terminales y puertas de un vuelo por código ICAO (p. ej. `RYR963`). |
| `estado_llegada` | Resumen de llegada: fecha, hora prevista/estimada/real, retraso, terminal y puerta. |
| `vuelos_por_ruta` | Vuelos entre dos aeropuertos IATA (p. ej. `MAD` → `TFS`). |
| `llegadas_aeropuerto` | Llegadas a un aeropuerto, filtrables por estado (`scheduled`, `active`, `landed`…). |
| `buscar_aeropuerto` | Busca por nombre o IATA (no por ciudad). |
| `buscar_aerolinea` | Busca por nombre o código para resolver su IATA. |

Si AviationStack rechaza una llamada (plan, clave, límite) el motivo llega al agente como error MCP, y si un vuelo no existe se le indica con un mensaje claro.

## Requisitos

- .NET 10 SDK y Aspire.
- Una clave de [AviationStack](https://aviationstack.com) (el plan gratuito limita parte de los endpoints).
- Un recurso de Microsoft Foundry con un modelo desplegado y `az login` en el tenant correcto.
  Esta demo usa `gpt-6.1-terra`, que exige la **Responses API** cuando hay tools.
- Para publicar: Azure CLI, `azd` (con la extensión `azure.ai.agents`) y Docker.

## Ejecución local con Aspire

Configura los parámetros del AppHost (user-secrets):

```bash
cd src/GlobalDemo.AppHost
dotnet user-secrets set Parameters:aviationstack-key <clave>
dotnet user-secrets set Parameters:openai-endpoint https://<recurso>.openai.azure.com/
dotnet user-secrets set Parameters:openai-deployment <nombre-del-despliegue>
dotnet run
```

El agente autentica contra Foundry con `DefaultAzureCredential` (tu `az login`). Abre el dashboard de Aspire para ver los puertos.

Pruébalo (sustituye `PUERTO` por el del servicio `agent`):

```bash
curl -H 'Content-Type: application/json' \
  -d '{"message":"¿Cuál es el estado del vuelo RYR963?"}' \
  http://localhost:PUERTO/chat
```

La respuesta incluye `reply`, `conversationId` (envíalo de nuevo para continuar la conversación) y `toolsUsed` con las tools que invocó el modelo.
`GET /tools` lista las tools descubiertas en el MCP.

Ejemplos de preguntas:

- «Dame el estado de llegada del vuelo TOM1289.»
- «¿Qué vuelos hay de MAD a TFS?»
- «¿Qué vuelos han aterrizado en TFS?»
- «Busca el aeropuerto Reina Sofía.»
- «¿Cuál es el código IATA de Iberia?»

### Probar el MCP o la API directamente

La API expone `/api/flights`, `/api/flights/{flightIcao}`, `/api/flights/{flightIcao}/arrival`, `/api/airports` y `/api/airlines`.
El MCP responde en `/mcp` y exige `Accept: application/json, text/event-stream`:

```bash
curl -s -X POST http://localhost:PUERTO_MCP/mcp \
  -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"buscar_vuelo","arguments":{"numeroVuelo":"RYR963"}}}'
```

## Publicación en Azure

### 1. MCP en Azure Container Apps

```bash
AVIATIONSTACK_KEY=<clave> bash deploy/deploy-mcp.sh
```

El script crea (si no existen) un ACR, el entorno de Container Apps y la app `mcp-aviation`, construye la imagen en Azure y guarda la
clave de AviationStack como secreto. Genera una clave nueva para `/mcp` en cada ejecución (rota la anterior) y deja `MCP_URL` y
`MCP_SUBSCRIPTION_KEY` en el entorno de `azd`. Sin la cabecera `Ocp-Apim-Subscription-Key` el MCP responde `401`.

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST https://<fqdn>/mcp   # 401
```

### 2. Agente como Foundry Hosted Agent

```bash
azd provision
azd deploy
azd ai agent invoke
```

`GlobalDemo.AgentHosted` lee su configuración de variables de entorno (las define `azure.yaml`):

| Variable | Uso |
|---|---|
| `FOUNDRY_PROJECT_ENDPOINT` | La inyecta la plataforma. |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` / `FOUNDRY_MODEL` | Despliegue del modelo. |
| `MCP_URL` | URL del MCP (o del AI Gateway). |
| `MCP_SUBSCRIPTION_KEY` | Se envía como `Ocp-Apim-Subscription-Key`. |

Detalles a tener en cuenta:

- Se expone el protocolo **Responses** en el puerto 8088 con `AgentHost.CreateBuilder` y `MapFoundryResponses()`.
  Hay que registrar el agente con `builder.WebApplicationBuilder.AddFoundryResponses(agent)`; la sobrecarga sobre `IServiceCollection`
  provoca `424 session_not_ready` en modo hospedado.
- El agente usa una identidad de agente propia en Entra ID; necesita acceso al modelo.
- Para ver trazas en el portal hay que conectar Application Insights al proyecto.
- Las tools se cargan dentro del contenedor con `McpClient`, así que Foundry **no las lista** en la configuración del agente
  (sí aparecen como `function_call` en las respuestas y en las trazas). Para verlas como recurso del proyecto habría que registrarlas
  en un **Toolbox** de Foundry (pendiente).

## Demo: bloquear una tool desde el AI Gateway

El agente hospedado llama al MCP a través de API Management (AI Gateway de Foundry):

```
Consola ──► Hosted agent ──► API Management (AI Gateway) ──► MCP en Container Apps
```

- La tool `aviation-mcp` se registró en Foundry (Compilación > Herramientas > Personalizadas > Model Context Protocol, clave en la cabecera `x-mcp-key`). Con el gateway habilitado, su endpoint es el de API Management.
- El agente define las tools con esquema fijo y abre una sesión MCP por llamada, así que si el gateway bloquea, el agente sigue arrancando y el 403 llega al modelo como resultado de la tool (`status: "blocked"`).
- `MCP_URL` es la URL del gateway y `MCP_KEY_HEADER` es `x-mcp-key` (`azd env set`, y `azd deploy`).
- El bloqueo es una policy en el servidor MCP de API Management (`deploy/gateway-policy.xml`) que devuelve 403 si el named value `mcp-blocked` vale `true`. La misma policy valida la clave del cliente e inyecta la de backend; se aplica con `bash deploy/lock-backend.sh`.

```bash
dotnet run --project src/GlobalDemo.DemoClient              # conversación; verás [TOOL] y [RESULT]
bash deploy/gateway-switch.sh all on                         # bloquea todo el servidor MCP (403)
bash deploy/gateway-switch.sh all off
bash deploy/gateway-switch.sh tools buscar_vuelo,estado_llegada   # bloquea solo esas tools; el resto siguen
bash deploy/gateway-switch.sh tools none
bash deploy/gateway-switch.sh status
```

El bloqueo por tool lee el nombre de la tool del `tools/call` (JSON-RPC) en la policy: las demás tools y `tools/list` siguen funcionando. Si cambias la policy, aplícala con `bash deploy/apply-gateway-policy.sh`.

Con el bloqueo activo, la siguiente pregunta muestra `[TOOL]` y `[BLOCKED]` en rojo sin redesplegar, y el modelo explica el bloqueo.
Ensáyalo antes de la demo: el tiempo de propagación del bloqueo no está documentado. API Management Basic v2 se factura por horas: bórralo al terminar.

## Panel de control (React)

Una interfaz web para la demo: chat con el agente (streaming), traza de la última llamada, interruptores del AI Gateway (todo el
servidor o cada tool) y «Probar sin agente», con el registro de peticiones.

```bash
bash deploy/run-control.sh        # http://127.0.0.1:5200
```

- El backend (`src/GlobalDemo.Control`) habla con el hosted agent y con API Management usando tu `az login`, y lee la configuración de `azd`.
  Ninguna clave ni token llega al navegador, y solo escucha en localhost.
- El frontend está en `src/GlobalDemo.Control/web` (Vite + React + TypeScript). El script lo compila a `wwwroot` la primera vez;
  para desarrollar: `npm run dev` en esa carpeta (proxy de `/api` al 5200).
- «Vía AI Gateway / Directo al backend» solo afecta a «Probar sin agente»: el agente siempre pasa por el gateway. En modo directo
  la llamada llega al MCP de Container Apps con la clave del cliente y debe dar 401, porque el backend está cerrado.
- Los interruptores escriben las named values `mcp-blocked` y `mcp-blocked-tools` de API Management, igual que `deploy/gateway-switch.sh`;
  el panel refresca el estado cada pocos segundos, así que también refleja cambios hechos desde el terminal.

## Seguridad

- La clave de AviationStack nunca va en el repositorio: user-secrets en local, secreto de Container Apps en Azure.
- `/mcp` (Container Apps) solo acepta la clave de backend en `x-backend-key` (`Mcp:ApiKey`), que únicamente conoce el AI Gateway. Los clientes presentan su clave en `x-mcp-key` y la valida el gateway (`deploy/gateway-policy.xml`), así que llamar al MCP directamente no se salta el bloqueo. `deploy/lock-backend.sh` lo configura. Sin `Mcp:ApiKey` queda abierto (uso local).
- `/api` (GlobalDemo.Api) se protege igual con `Api:ApiKey` y la misma cabecera. Sin configurarla, queda abierto (uso local).
- Para consultar la clave de los clientes del MCP: `azd env get-value MCP_SUBSCRIPTION_KEY` (no la pegues en el repositorio ni en chats).
- La autenticación con Foundry usa Entra ID, sin claves de API.

## Notas

- El cliente de AviationStack envía `access_key` en la query string; no registres URLs completas en logs ni trazas.
- Las horas que devuelve AviationStack llevan `+00:00`, pero pueden ser locales del aeropuerto (no confirmado). Compáralas antes de fiarte de ellas.
- Las tools usan códigos **ICAO** para vuelos (`RYR963`) e **IATA** para aeropuertos (`TFS`).
- El plan gratuito de AviationStack tiene límites de peticiones; la caché de 2 minutos reduce el consumo.

## Siguiente fase

Poner el MCP detrás de un **AI Gateway** (Azure API Management), apuntar `MCP_URL` / `Agent__McpUrl` al gateway, y cortar o monitorizar
una tool en directo desde Foundry. El agente ya captura los `403/429` del gateway y se lo cuenta al usuario en lugar de romperse.
