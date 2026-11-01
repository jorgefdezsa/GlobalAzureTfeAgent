using Azure.AI.AgentServer.Core;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

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
    http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

await using var mcp = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(mcpUrl), Name = "aviation" }, http));
var tools = await mcp.ListToolsAsync();
Console.WriteLine($"MCP {mcpUrl}: {tools.Count} tools ({string.Join(", ", tools.Select(t => t.Name))})");

const string instructions = """
    Eres el asistente de vuelos de Limusinas Tenerife. Ayudas al equipo a consultar vuelos, aeropuertos y aerolíneas
    para organizar recogidas de pasajeros. Usa siempre las herramientas para obtener datos de vuelos; no los inventes.
    Si un vuelo no aparece o una herramienta falla o no está disponible, dilo con claridad y sugiere una alternativa.
    Responde en español, breve y con las horas en formato local.
    """;

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: instructions,
        name: "AsistenteVuelos",
        tools: [.. tools.Cast<AITool>()]);

var builder = AgentHost.CreateBuilder(args);
// Sobrecarga sobre el builder (no sobre builder.Services): en Foundry hospedado la otra lanza InvalidOperationException.
builder.WebApplicationBuilder.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();
