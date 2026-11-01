using GlobalDemo.Aviation.Models;
using Microsoft.Extensions.Caching.Hybrid;

namespace GlobalDemo.Aviation;

/// <summary>Capa de dominio: caché y reglas sobre el cliente. La usan la API y el servidor MCP.</summary>
public sealed class AviationService(AviationStackClient client, HybridCache cache)
{
    private static readonly HybridCacheEntryOptions Short = new() { Expiration = TimeSpan.FromMinutes(2) };
    private static readonly HybridCacheEntryOptions Long = new() { Expiration = TimeSpan.FromHours(12) };

    public async Task<Page<Flight>> SearchFlightsAsync(FlightQuery q, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            $"flights:{q}", async token => await client.GetFlightsAsync(q, token), Short, cancellationToken: ct);

    /// <summary>
    /// Estado de un vuelo. Se consulta solo por flight_icao (p. ej. RYR963), sin flight_iata ni arr_iata,
    /// y entre los resultados se elige el que tiene la llegada prevista más cercana a ahora.
    /// </summary>
    public async Task<Flight?> GetFlightAsync(string flightIcao, string? date = null, CancellationToken ct = default)
    {
        var page = await SearchFlightsAsync(
            new FlightQuery(FlightIcao: flightIcao.Trim().ToUpperInvariant(), FlightDate: date, Limit: 100), ct);

        var now = DateTimeOffset.UtcNow;
        return page.Data
            .OrderBy(f => f.Arrival?.Scheduled is { } t ? Math.Abs((t - now).TotalMinutes) : double.MaxValue)
            .FirstOrDefault();
    }

    public async Task<Page<Airport>> SearchAirportsAsync(string search, int limit = 10, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            $"airports:{search}:{limit}", async token => await client.GetAirportsAsync(search, limit, 0, token), Long, cancellationToken: ct);

    public async Task<Page<Airline>> SearchAirlinesAsync(string search, int limit = 10, CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            $"airlines:{search}:{limit}", async token => await client.GetAirlinesAsync(search, limit, 0, token), Long, cancellationToken: ct);

    /// <summary>Resumen de llegada de un vuelo, pensado para decidir a qué hora recoger al pasajero.</summary>
    public async Task<ArrivalStatus?> GetArrivalStatusAsync(string flightIcao, string? date = null, CancellationToken ct = default)
    {
        var f = await GetFlightAsync(flightIcao, date, ct);
        if (f?.Arrival is null) return null;

        var a = f.Arrival;
        var expected = a.EstimatedRunway ?? a.Estimated ?? a.Scheduled;
        return new ArrivalStatus(
            flightIcao.Trim().ToUpperInvariant(), f.FlightDate, f.FlightStatus, a.Iata, a.Terminal, a.Gate,
            a.Scheduled, a.Estimated, a.Actual, a.EstimatedRunway, a.Delay, expected);
    }
}

public sealed record ArrivalStatus(
    string FlightIcao,
    string? FlightDate,
    string? Status,
    string? ArrivalAirport,
    string? Terminal,
    string? Gate,
    DateTimeOffset? Scheduled,
    DateTimeOffset? Estimated,
    DateTimeOffset? Actual,
    DateTimeOffset? EstimatedRunway,
    int? DelayMinutes,
    DateTimeOffset? ExpectedArrival);
