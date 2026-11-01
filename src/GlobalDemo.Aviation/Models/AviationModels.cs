using System.Text.Json.Serialization;

namespace GlobalDemo.Aviation.Models;

public sealed record Pagination(int Limit, int Offset, int Count, int Total);

public sealed record Page<T>(Pagination Pagination, IReadOnlyList<T> Data);

public sealed record FlightEndpoint(
    string? Airport,
    string? Timezone,
    string? Iata,
    string? Icao,
    string? Terminal,
    string? Gate,
    string? Baggage,
    int? Delay,
    DateTimeOffset? Scheduled,
    DateTimeOffset? Estimated,
    DateTimeOffset? Actual,
    DateTimeOffset? EstimatedRunway,
    DateTimeOffset? ActualRunway);

public sealed record AirlineRef(string? Name, string? Iata, string? Icao);

public sealed record FlightRef(string? Number, string? Iata, string? Icao);

public sealed record AircraftRef(string? Registration, string? Iata, string? Icao, string? Icao24);

public sealed record LiveInfo(
    DateTimeOffset? Updated,
    double? Latitude,
    double? Longitude,
    double? Altitude,
    double? Direction,
    double? SpeedHorizontal,
    double? SpeedVertical,
    bool? IsGround);

public sealed record Flight(
    string? FlightDate,
    string? FlightStatus,
    FlightEndpoint? Departure,
    FlightEndpoint? Arrival,
    AirlineRef? Airline,
    [property: JsonPropertyName("flight")] FlightRef? FlightNumber,
    AircraftRef? Aircraft,
    LiveInfo? Live);

public sealed record Airport(
    string? AirportName,
    string? IataCode,
    string? IcaoCode,
    string? Latitude,
    string? Longitude,
    string? Timezone,
    string? CountryName,
    string? CountryIso2,
    string? CityIataCode);

public sealed record Airline(
    string? AirlineName,
    string? IataCode,
    string? IcaoCode,
    string? Callsign,
    string? Status,
    string? FleetSize,
    string? CountryName);

public sealed record FlightQuery(
    string? FlightIata = null,
    string? FlightIcao = null,
    string? DepIata = null,
    string? ArrIata = null,
    string? AirlineIata = null,
    string? FlightStatus = null,
    string? FlightDate = null,
    int Limit = 10,
    int Offset = 0);
