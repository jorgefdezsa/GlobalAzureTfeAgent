using System.ComponentModel;
using GlobalDemo.Aviation;
using GlobalDemo.Aviation.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace GlobalDemo.Mcp;

[McpServerToolType]
public sealed class AviationTools(AviationService aviation)
{
    [McpServerTool(Name = "buscar_vuelo", ReadOnly = true)]
    [Description("Busca un vuelo por su código ICAO (por ejemplo RYR963 para Ryanair 963) y devuelve su estado, horarios, terminales y puertas.")]
    public Task<Flight> BuscarVuelo(
        [Description("Código ICAO del vuelo, p. ej. RYR963 (código ICAO de la aerolínea más el número)")] string numeroVuelo,
        [Description("Fecha del vuelo en formato yyyy-MM-dd (opcional)")] string? fecha = null,
        CancellationToken ct = default) =>
        Run(async () => await aviation.GetFlightAsync(numeroVuelo, fecha, ct) ?? throw NoEncontrado(numeroVuelo));

    [McpServerTool(Name = "estado_llegada", ReadOnly = true)]
    [Description("Resumen de la llegada de un vuelo: fecha del vuelo, hora prevista, estimada y real, retraso en minutos, terminal y puerta. Útil para decidir a qué hora recoger a un pasajero. Comprueba flightDate: sin fecha se devuelve el vuelo más cercano a hoy, que puede ser de otro día.")]
    public Task<ArrivalStatus> EstadoLlegada(
        [Description("Código ICAO del vuelo, p. ej. RYR963 (código ICAO de la aerolínea más el número)")] string numeroVuelo,
        [Description("Fecha del vuelo en formato yyyy-MM-dd (opcional)")] string? fecha = null,
        CancellationToken ct = default) =>
        Run(async () => await aviation.GetArrivalStatusAsync(numeroVuelo, fecha, ct) ?? throw NoEncontrado(numeroVuelo));

    [McpServerTool(Name = "vuelos_por_ruta", ReadOnly = true)]
    [Description("Lista vuelos entre dos aeropuertos identificados por su código IATA (p. ej. MAD y TFS).")]
    public Task<Page<Flight>> VuelosPorRuta(
        [Description("IATA del aeropuerto de salida")] string origen,
        [Description("IATA del aeropuerto de llegada")] string destino,
        [Description("Máximo de resultados (1-50)")] int limite = 10,
        CancellationToken ct = default) =>
        Run(() => aviation.SearchFlightsAsync(new FlightQuery(DepIata: origen.ToUpperInvariant(), ArrIata: destino.ToUpperInvariant(), Limit: Math.Clamp(limite, 1, 50)), ct));

    [McpServerTool(Name = "llegadas_aeropuerto", ReadOnly = true)]
    [Description("Lista vuelos que llegan a un aeropuerto, opcionalmente filtrados por estado (scheduled, active, landed, cancelled, incident, diverted).")]
    public Task<Page<Flight>> LlegadasAeropuerto(
        [Description("IATA del aeropuerto, p. ej. TFS")] string iata,
        [Description("Estado del vuelo (opcional)")] string? estado = null,
        [Description("Máximo de resultados (1-50)")] int limite = 10,
        CancellationToken ct = default) =>
        Run(() => aviation.SearchFlightsAsync(new FlightQuery(ArrIata: iata.ToUpperInvariant(), FlightStatus: estado, Limit: Math.Clamp(limite, 1, 50)), ct));

    [McpServerTool(Name = "buscar_aeropuerto", ReadOnly = true)]
    [Description("Busca aeropuertos por el nombre del aeropuerto (p. ej. 'Reina Sofia', 'Barajas') o por su código IATA. No busca por ciudad: 'Tenerife' o 'Madrid' no devuelven resultados.")]
    public Task<Page<Airport>> BuscarAeropuerto(
        [Description("Nombre del aeropuerto o código IATA, p. ej. 'Reina Sofia' o 'TFS'")] string texto,
        CancellationToken ct = default) =>
        Run(() => aviation.SearchAirportsAsync(texto, 10, ct));

    [McpServerTool(Name = "buscar_aerolinea", ReadOnly = true)]
    [Description("Busca aerolíneas por nombre o código para resolver su código IATA.")]
    public Task<Page<Airline>> BuscarAerolinea(
        [Description("Texto a buscar, p. ej. 'Iberia'")] string texto,
        CancellationToken ct = default) =>
        Run(() => aviation.SearchAirlinesAsync(texto, 10, ct));

    private static McpException NoEncontrado(string vuelo) =>
        new($"No se encontró ningún vuelo con código ICAO '{vuelo}'. Revisa el código (aerolínea ICAO + número, p. ej. RYR963) o prueba con otra fecha.");

    /// <summary>Si AviationStack rechaza la llamada (plan, clave, límite), el motivo llega al agente en vez de un error genérico.</summary>
    private static async Task<T> Run<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AviationStackException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
