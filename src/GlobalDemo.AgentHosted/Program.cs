using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using GlobalDemo.AgentHosted;

// Agente hospedado en Foundry: expone /responses (protocolo Responses) en el puerto 8088.
// Foundry inyecta FOUNDRY_PROJECT_ENDPOINT y el nombre del despliegue; MCP_URL lo declara agent.yaml.
var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT no está definida."));
var deployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL")
    ?? Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Falta FOUNDRY_MODEL o AZURE_AI_MODEL_DEPLOYMENT_NAME.");
var mcpUrl = Environment.GetEnvironmentVariable("MCP_URL")
    ?? throw new InvalidOperationException("MCP_URL no está definida: URL pública del servidor MCP (p. ej. https://.../mcp).");

// En la parte 2, MCP_URL apunta al AI Gateway; si pide clave de suscripción, MCP_SUBSCRIPTION_KEY.
var http = new HttpClient();
if (Environment.GetEnvironmentVariable("MCP_SUBSCRIPTION_KEY") is { Length: > 0 } key)
    http.DefaultRequestHeaders.Add(Environment.GetEnvironmentVariable("MCP_KEY_HEADER") is { Length: > 0 } header ? header : "Ocp-Apim-Subscription-Key", key);

// Las tools tienen esquema fijo y el MCP solo se llama al usarlas: arrancar no depende de que el gateway esté abierto.
var tools = new AviationMcpTools(new Uri(mcpUrl), http).CreateTools();
Console.WriteLine($"MCP {mcpUrl}: {tools.Count} tools de esquema fijo");

const string instructions = """
    Eres el asistente de vuelos de Limusinas Tenerife. Ayudas al equipo a consultar vuelos, aeropuertos y aerolíneas
    para organizar recogidas de pasajeros. Usa siempre las herramientas para obtener datos de vuelos; no los inventes.
    Si un vuelo no aparece o una herramienta falla o no está disponible, dilo con claridad y sugiere una alternativa.
    Si el resultado de una herramienta tiene status "blocked", di que la herramienta está bloqueada por el administrador,
    cita el mensaje y no reintentes.
    Si el resultado tiene status "rate_limited", di que el AI Gateway ha alcanzado el límite de llamadas por minuto,
    que hay que esperar un poco, y no reintentes en bucle ni llames a otras herramientas para esquivarlo.
    Responde en español, breve y con las horas en formato local.
    """;

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: instructions,
        name: "AsistenteVuelos",
        tools: tools);

var builder = AgentHost.CreateBuilder(args);
// Sobrecarga sobre el builder (no sobre builder.Services): en Foundry hospedado la otra lanza InvalidOperationException.
builder.WebApplicationBuilder.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
