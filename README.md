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

## Seguridad

- La clave de AviationStack nunca va en el repositorio: user-secrets en local, secreto de Container Apps en Azure.
- `/mcp` se protege con una clave opcional (`Mcp:ApiKey`, comparación en tiempo constante). Sin configurarla, queda abierto (uso local).
- `/api` (GlobalDemo.Api) se protege igual con `Api:ApiKey` y la misma cabecera. Sin configurarla, queda abierto (uso local).
- Para consultar la clave del MCP publicado: `azd env get-value MCP_SUBSCRIPTION_KEY` (no la pegues en el repositorio ni en chats).
- La autenticación con Foundry usa Entra ID, sin claves de API.

## Notas

- El cliente de AviationStack envía `access_key` en la query string; no registres URLs completas en logs ni trazas.
- Las horas que devuelve AviationStack llevan `+00:00`, pero pueden ser locales del aeropuerto (no confirmado). Compáralas antes de fiarte de ellas.
- Las tools usan códigos **ICAO** para vuelos (`RYR963`) e **IATA** para aeropuertos (`TFS`).
- El plan gratuito de AviationStack tiene límites de peticiones; la caché de 2 minutos reduce el consumo.

## Siguiente fase

Poner el MCP detrás de un **AI Gateway** (Azure API Management), apuntar `MCP_URL` / `Agent__McpUrl` al gateway, y cortar o monitorizar
una tool en directo desde Foundry. El agente ya captura los `403/429` del gateway y se lo cuenta al usuario en lugar de romperse.
