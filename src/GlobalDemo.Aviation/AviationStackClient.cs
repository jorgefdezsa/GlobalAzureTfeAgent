using System.Net.Http.Json;
using System.Text.Json;
using GlobalDemo.Aviation.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GlobalDemo.Aviation;

/// <summary>Cliente tipado sobre la API REST de AviationStack. Sin caché ni reglas de negocio.</summary>
public sealed class AviationStackClient(HttpClient http, IOptions<AviationStackOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string _accessKey = options.Value.AccessKey;

    public Task<Page<Flight>> GetFlightsAsync(FlightQuery q, CancellationToken ct = default) =>
        GetAsync<Flight>("flights", new Dictionary<string, string?>
        {
            ["flight_iata"] = q.FlightIata,
            ["flight_icao"] = q.FlightIcao,
            ["dep_iata"] = q.DepIata,
            ["arr_iata"] = q.ArrIata,
            ["airline_iata"] = q.AirlineIata,
            ["flight_status"] = q.FlightStatus,
            ["flight_date"] = q.FlightDate,
            ["limit"] = q.Limit.ToString(),
            ["offset"] = q.Offset.ToString(),
        }, ct);

    public Task<Page<Airport>> GetAirportsAsync(string? search, int limit = 10, int offset = 0, CancellationToken ct = default) =>
        GetAsync<Airport>("airports", new Dictionary<string, string?>
        {
            ["search"] = search,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString(),
        }, ct);

    public Task<Page<Airline>> GetAirlinesAsync(string? search, int limit = 10, int offset = 0, CancellationToken ct = default) =>
        GetAsync<Airline>("airlines", new Dictionary<string, string?>
        {
            ["search"] = search,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString(),
        }, ct);

    private async Task<Page<T>> GetAsync<T>(string path, Dictionary<string, string?> query, CancellationToken ct)
    {
        query["access_key"] = _accessKey;
        var url = QueryHelpers.AddQueryString(path, query.Where(kv => !string.IsNullOrEmpty(kv.Value)));

        using var response = await http.GetAsync(url, ct);
        Envelope<T>? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, ct);
        }
        catch (JsonException) when (!response.IsSuccessStatusCode)
        {
            // Cuerpo de error que no es JSON (p. ej. un 502 con HTML): cae en EnsureSuccessStatusCode.
        }

        if (body?.Error is { } error)
            throw new AviationStackException(error.Code ?? "unknown", error.Message ?? "Error sin mensaje");

        response.EnsureSuccessStatusCode();
        return new Page<T>(body?.Pagination ?? new Pagination(0, 0, 0, 0), body?.Data ?? []);
    }

    private sealed record Envelope<T>(Pagination? Pagination, List<T>? Data, ApiError? Error);

    private sealed record ApiError(string? Code, string? Message);
}
