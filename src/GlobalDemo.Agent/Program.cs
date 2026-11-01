using System.Collections.Concurrent;
using System.ClientModel.Primitives;
using Azure.Identity;
using GlobalDemo.Agent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOptions<AgentOptions>()
    .BindConfiguration(AgentOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// El nombre "mcp" lo resuelve la service discovery de Aspire; HttpClient sale de IHttpClientFactory.
// Sustituye la resiliencia por defecto de ServiceDefaults (10 s por intento): una tool puede tardar más
// (AviationStack, arranque en frío) y no debe cortarse como si el MCP estuviera caído.
builder.Services.AddHttpClient("mcp")
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
    });

// Agente y cliente MCP son singleton: se conectan una vez, en el primer uso.
builder.Services.AddSingleton<AgentHost>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Una sesión por conversación (en memoria; suficiente para la demo).
var sessions = new ConcurrentDictionary<string, AgentSession>();

app.MapPost("/chat", async (ChatRequest req, AgentHost host, CancellationToken ct) =>
{
    var agent = await host.GetAgentAsync(ct);
    var conversationId = req.ConversationId ?? Guid.NewGuid().ToString("N");

    if (!sessions.TryGetValue(conversationId, out var session))
        sessions[conversationId] = session = await agent.CreateSessionAsync(ct);

    try
    {
        var response = await agent.RunAsync(req.Message, session, cancellationToken: ct);
        var toolsUsed = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Select(c => new ToolCall(c.Name, c.Arguments))
            .ToList();
        return Results.Ok(new ChatResponse(conversationId, response.Text, toolsUsed));
    }
    catch (Exception ex) when (((ex is TimeoutException or TaskCanceledException) && !ct.IsCancellationRequested)
                               || ex is ModelContextProtocol.McpException)
    {
        app.Logger.LogWarning(ex, "Fallo al invocar una tool del MCP");
        return Results.Ok(new ChatResponse(conversationId,
            "No he podido completar la consulta: la herramienta de vuelos tardó demasiado o no respondió. Inténtalo de nuevo en unos segundos.", []));
    }
    catch (HttpRequestException ex)
    {
        // Si el gateway corta una tool (403/429) el agente lo cuenta en vez de romperse.
        return Results.Ok(new ChatResponse(conversationId,
            $"No he podido completar la consulta: el acceso a una herramienta está restringido o no disponible ({ex.StatusCode}).", []));
    }
});

app.MapGet("/tools", async (AgentHost host, CancellationToken ct) =>
    (await host.GetToolsAsync(ct)).Select(t => new { t.Name, t.Description }));

app.Run();

internal sealed record ChatRequest(string Message, string? ConversationId);

internal sealed record ChatResponse(string ConversationId, string Reply, IReadOnlyList<ToolCall> ToolsUsed);

internal sealed record ToolCall(string Name, IDictionary<string, object?>? Arguments);

/// <summary>Construye perezosamente el cliente MCP y el agente con sus tools.</summary>
internal sealed class AgentHost(
    Microsoft.Extensions.Options.IOptions<AgentOptions> options,
    IHttpClientFactory httpFactory,
    ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private const string Instructions = """
        Eres el asistente de vuelos de Limusinas Tenerife. Ayudas al equipo a consultar vuelos, aeropuertos y aerolíneas
        para organizar recogidas de pasajeros. Usa siempre las herramientas para obtener datos de vuelos; no los inventes.
        Si un vuelo no aparece o una herramienta falla o no está disponible, dilo con claridad y sugiere una alternativa
        Responde en español, breve y con las horas en formato local.
        """;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _mcp;
    private IList<McpClientTool>? _tools;
    private AIAgent? _agent;

    public async Task<IList<McpClientTool>> GetToolsAsync(CancellationToken ct)
    {
        await EnsureAsync(ct);
        return _tools!;
    }

    public async Task<AIAgent> GetAgentAsync(CancellationToken ct)
    {
        await EnsureAsync(ct);
        return _agent!;
    }

    private async Task EnsureAsync(CancellationToken ct)
    {
        if (_agent is not null) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_agent is not null) return;
            var o = options.Value;

            var http = httpFactory.CreateClient("mcp");
            if (!string.IsNullOrEmpty(o.McpSubscriptionKey))
                http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", o.McpSubscriptionKey);

            _mcp = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(o.McpUrl), Name = "aviation" }, http),
                loggerFactory: loggerFactory,
                cancellationToken: ct);
            _tools = await _mcp.ListToolsAsync(cancellationToken: ct);

            // Ruta /openai/v1/ de Azure OpenAI: no depende de api-version y autentica con Entra ID.
            var openAi = new OpenAIClient(
                new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"),
                new OpenAIClientOptions { Endpoint = new Uri(new Uri(o.OpenAiEndpoint), "openai/v1/") });

            _agent = openAi
                .GetResponsesClient()
                .AsIChatClient(o.Deployment)
                .AsAIAgent(
                    instructions: Instructions,
                    name: "AsistenteVuelos",
                    tools: [.. _tools.Cast<AITool>()]);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcp is not null) await _mcp.DisposeAsync();
    }
}
