using System.Security.Cryptography;
using System.Text;
using GlobalDemo.Aviation;
using GlobalDemo.Aviation.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddAviation();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

// Si hay Api:ApiKey (p. ej. publicada en Azure), /api exige la cabecera Ocp-Apim-Subscription-Key.
// Sin clave configurada (local) no se pide nada.
var apiKey = app.Configuration["Api:ApiKey"];
if (!string.IsNullOrEmpty(apiKey))
{
    var expected = Encoding.UTF8.GetBytes(apiKey);
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            var sent = Encoding.UTF8.GetBytes(ctx.Request.Headers["Ocp-Apim-Subscription-Key"].ToString());
            if (!CryptographicOperations.FixedTimeEquals(sent, expected))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await next();
    });
}

var api = app.MapGroup("/api");

// Un rechazo de AviationStack (parámetro inválido, plan, cuota) no debe ser un 500 vacío.
api.AddEndpointFilter(async (ctx, next) =>
{
    try
    {
        return await next(ctx);
    }
    catch (AviationStackException ex)
    {
        var status = ex.Code is "validation_error" or "invalid_flight_status" ? StatusCodes.Status400BadRequest : StatusCodes.Status502BadGateway;
        return Results.Problem(detail: ex.Message, statusCode: status, title: "AviationStack rechazó la consulta");
    }
});

api.MapGet("/flights", (AviationService svc, string? flightIata, string? flightIcao, string? depIata, string? arrIata,
        string? airlineIata, string? status, string? date, int limit = 50, int offset = 0, CancellationToken ct = default) =>
    svc.SearchFlightsAsync(new FlightQuery(
        FlightIata: flightIata, FlightIcao: flightIcao, DepIata: depIata, ArrIata: arrIata,
        AirlineIata: airlineIata, FlightStatus: status, FlightDate: date,
        Limit: Math.Clamp(limit, 1, 50), Offset: Math.Max(offset, 0)), ct));

api.MapGet("/flights/{flightIcao}", async (AviationService svc, string flightIcao, string? date, CancellationToken ct) =>
    await svc.GetFlightAsync(flightIcao, date, ct) is { } f ? Results.Ok(f) : Results.NotFound());

api.MapGet("/flights/{flightIcao}/arrival", async (AviationService svc, string flightIcao, string? date, CancellationToken ct) =>
    await svc.GetArrivalStatusAsync(flightIcao, date, ct) is { } s ? Results.Ok(s) : Results.NotFound());

api.MapGet("/airports", (AviationService svc, string search, int limit = 50, CancellationToken ct = default) =>
    svc.SearchAirportsAsync(search, Math.Clamp(limit, 1, 50), ct));

api.MapGet("/airlines", (AviationService svc, string search, int limit = 50, CancellationToken ct = default) =>
    svc.SearchAirlinesAsync(search, Math.Clamp(limit, 1, 50), ct));

app.Run();
