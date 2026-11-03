using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace GlobalDemo.Control;

/// <summary>Configuración del panel (sección Control, variables Control__*). Nada de esto llega al navegador.</summary>
public sealed class ControlOptions
{
    public string SubscriptionId { get; set; } = "";
    public string ResourceGroup { get; set; } = "GlobalAzureTenerife";
    public string Apim { get; set; } = "apim-globaltfe";

    /// <summary>Endpoint Responses del hosted agent (AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT de azd).</summary>
    public string AgentEndpoint { get; set; } = "";

    /// <summary>URL del servidor MCP en el AI Gateway y la del backend en Container Apps (para demostrar que está cerrado).</summary>
    public string McpGatewayUrl { get; set; } = "";
    public string McpDirectUrl { get; set; } = "";

    /// <summary>Clave del cliente (x-mcp-key), la misma del agente y la conexión de Foundry.</summary>
    public string McpKey { get; set; } = "";
}

public sealed record ChatRequest(string Message, string? PreviousResponseId);

public sealed record ToolTestRequest(string Tool, JsonNode? Arguments, string Mode);

public sealed record BlockAllRequest(bool Blocked);

public sealed record BlockToolsRequest(string[] Tools);

public sealed record ToolInfo(string Name, string Description, JsonNode SampleArgs);

public sealed record BlockState(bool BlockedAll, string[] BlockedTools);

public sealed record LogEntry(DateTimeOffset At, string Kind, string Detail, int? Status, long Ms, string Outcome);

/// <summary>Últimas peticiones del panel, en memoria.</summary>
public sealed class RequestLog
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public void Add(string kind, string detail, int? status, long ms, string outcome)
    {
        _entries.Enqueue(new LogEntry(DateTimeOffset.Now, kind, detail.Length > 90 ? detail[..90] + "…" : detail, status, ms, outcome));
        while (_entries.Count > 40) _entries.TryDequeue(out _);
    }

    public LogEntry[] Latest() => [.. _entries.Reverse()];
}

public static class Catalog
{
    public static readonly ToolInfo[] Tools =
    [
        new("buscar_vuelo", "Estado, horarios, terminales y puertas de un vuelo por código ICAO", JsonNode.Parse("""{"numeroVuelo":"RYR963"}""")!),
        new("estado_llegada", "Resumen de la llegada: hora prevista, estimada, retraso, terminal y puerta", JsonNode.Parse("""{"numeroVuelo":"RYR963"}""")!),
        new("vuelos_por_ruta", "Vuelos entre dos aeropuertos IATA", JsonNode.Parse("""{"origen":"MAD","destino":"TFS","limite":3}""")!),
        new("llegadas_aeropuerto", "Llegadas a un aeropuerto, filtrables por estado", JsonNode.Parse("""{"iata":"TFS","limite":3}""")!),
        new("buscar_aeropuerto", "Busca aeropuertos por nombre o IATA", JsonNode.Parse("""{"texto":"Reina Sofia"}""")!),
        new("buscar_aerolinea", "Busca aerolíneas por nombre o código", JsonNode.Parse("""{"texto":"Iberia"}""")!),
    ];
}
