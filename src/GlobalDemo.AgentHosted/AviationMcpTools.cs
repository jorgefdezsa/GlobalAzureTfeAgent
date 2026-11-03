using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace GlobalDemo.AgentHosted;

/// <summary>
/// Tools de vuelos con esquema fijo en el código: el modelo siempre las ve, aunque el gateway esté cerrando el MCP.
/// Cada ejecución abre una sesión MCP, llama a la tool y la cierra. Si el gateway la rechaza (403, 429…) o el MCP
/// devuelve isError, el motivo vuelve como resultado de la tool para que el modelo lo explique, sin romper el agente.
/// </summary>
internal sealed class AviationMcpTools(Uri endpoint, HttpClient http)
{
    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(BuscarVuelo, "buscar_vuelo",
            "Busca un vuelo por su código ICAO (por ejemplo RYR963 para Ryanair 963) y devuelve su estado, horarios, terminales y puertas."),
        AIFunctionFactory.Create(EstadoLlegada, "estado_llegada",
            "Resumen de la llegada de un vuelo: fecha del vuelo, hora prevista, estimada y real, retraso en minutos, terminal y puerta. Útil para decidir a qué hora recoger a un pasajero. Comprueba flightDate: sin fecha se devuelve el vuelo más cercano a hoy, que puede ser de otro día."),
        AIFunctionFactory.Create(VuelosPorRuta, "vuelos_por_ruta",
            "Lista vuelos entre dos aeropuertos identificados por su código IATA (p. ej. MAD y TFS)."),
        AIFunctionFactory.Create(LlegadasAeropuerto, "llegadas_aeropuerto",
            "Lista vuelos que llegan a un aeropuerto, opcionalmente filtrados por estado (scheduled, active, landed, cancelled, incident, diverted)."),
        AIFunctionFactory.Create(BuscarAeropuerto, "buscar_aeropuerto",
            "Busca aeropuertos por el nombre del aeropuerto (p. ej. 'Reina Sofia', 'Barajas') o por su código IATA. No busca por ciudad: 'Tenerife' o 'Madrid' no devuelven resultados."),
        AIFunctionFactory.Create(BuscarAerolinea, "buscar_aerolinea",
            "Busca aerolíneas por nombre o código para resolver su código IATA."),
    ];

    private Task<string> BuscarVuelo(
        [Description("Código ICAO del vuelo, p. ej. RYR963 (código ICAO de la aerolínea más el número)")] string numeroVuelo,
        [Description("Fecha del vuelo en formato yyyy-MM-dd (opcional)")] string? fecha = null,
        CancellationToken ct = default) =>
        CallAsync("buscar_vuelo", new() { ["numeroVuelo"] = numeroVuelo, ["fecha"] = fecha }, ct);

    private Task<string> EstadoLlegada(
        [Description("Código ICAO del vuelo, p. ej. RYR963 (código ICAO de la aerolínea más el número)")] string numeroVuelo,
        [Description("Fecha del vuelo en formato yyyy-MM-dd (opcional)")] string? fecha = null,
        CancellationToken ct = default) =>
        CallAsync("estado_llegada", new() { ["numeroVuelo"] = numeroVuelo, ["fecha"] = fecha }, ct);

    private Task<string> VuelosPorRuta(
        [Description("IATA del aeropuerto de salida")] string origen,
        [Description("IATA del aeropuerto de llegada")] string destino,
        [Description("Máximo de resultados (1-50)")] int limite = 10,
        CancellationToken ct = default) =>
        CallAsync("vuelos_por_ruta", new() { ["origen"] = origen, ["destino"] = destino, ["limite"] = limite }, ct);

    private Task<string> LlegadasAeropuerto(
        [Description("IATA del aeropuerto, p. ej. TFS")] string iata,
        [Description("Estado del vuelo (opcional)")] string? estado = null,
        [Description("Máximo de resultados (1-50)")] int limite = 10,
        CancellationToken ct = default) =>
        CallAsync("llegadas_aeropuerto", new() { ["iata"] = iata, ["estado"] = estado, ["limite"] = limite }, ct);

    private Task<string> BuscarAeropuerto(
        [Description("Nombre del aeropuerto o código IATA, p. ej. 'Reina Sofia' o 'TFS'")] string texto,
        CancellationToken ct = default) =>
        CallAsync("buscar_aeropuerto", new() { ["texto"] = texto }, ct);

    private Task<string> BuscarAerolinea(
        [Description("Texto a buscar, p. ej. 'Iberia'")] string texto,
        CancellationToken ct = default) =>
        CallAsync("buscar_aerolinea", new() { ["texto"] = texto }, ct);

    private async Task<string> CallAsync(string tool, Dictionary<string, object?> args, CancellationToken ct)
    {
        // Los parámetros opcionales sin valor no se envían: el MCP usa su valor por defecto.
        IReadOnlyDictionary<string, object?> arguments = args.Where(a => a.Value is not null).ToDictionary(a => a.Key, a => a.Value);
        try
        {
            await using var mcp = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint, Name = "aviation" }, http),
                cancellationToken: ct);
            var result = await mcp.CallToolAsync(tool, arguments, cancellationToken: ct);
            var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
            return result.IsError == true ? Failure(tool, "tool_error", text) : text;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            // El gateway bloqueó la tool: el mensaje incluye el cuerpo de la respuesta con el motivo.
            return Failure(tool, "blocked", ex.Message);
        }
        catch (HttpRequestException ex)
        {
            // Otro corte del gateway (429, 5xx…) o el MCP no es alcanzable.
            return Failure(tool, "gateway_error", ex.Message);
        }
        catch (McpException ex)
        {
            return Failure(tool, "tool_error", ex.Message);
        }
    }

    // status: "blocked" | "gateway_error" | "tool_error". El cliente de la demo y las instrucciones del agente dependen de estos valores.
    private static string Failure(string tool, string status, string message) =>
        JsonSerializer.Serialize(new { status, tool, message });
}
