using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using GlobalDemo.Control;

// Panel de control de la demo: habla con el hosted agent y con API Management en nombre del navegador, para que ninguna
// clave ni token salga del equipo. Escucha solo en localhost y usa la identidad de `az login`.
var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5200");

var options = builder.Configuration.GetSection("Control").Get<ControlOptions>() ?? new ControlOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<RequestLog>();
builder.Services.AddSingleton<ArmClient>();
builder.Services.AddSingleton<AuditClient>();
builder.Services.AddHttpClient("agent", c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient("mcp", c => c.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient("arm", c => c.Timeout = TimeSpan.FromSeconds(30));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/tools", () => Catalog.Tools);

app.MapGet("/api/state", async (ArmClient arm, ControlOptions o, CancellationToken ct) =>
{
    try
    {
        var state = await arm.ReadAsync(ct);
        return Results.Ok(new { state.BlockedAll, state.BlockedTools, state.RateLimit, AgentConfigured = o.AgentEndpoint.Length > 0, Apim = o.Apim, PriceInput = o.PriceInputPerMillion, PriceCached = o.PriceCachedPerMillion, PriceOutput = o.PriceOutputPerMillion });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/guardrail", async (ArmClient arm, CancellationToken ct) =>
{
    try { return Results.Ok(await arm.ReadGuardrailAsync(ct)); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway); }
});

app.MapPost("/api/blocks/all", async (BlockAllRequest req, ArmClient arm, RequestLog log, CancellationToken ct) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        await arm.SetAllAsync(req.Blocked, ct);
        log.Add("gateway", req.Blocked ? "Bloquear todo el servidor MCP" : "Desbloquear todo el servidor MCP", 200, sw.ElapsedMilliseconds, req.Blocked ? "bloqueado" : "ok");
        return Results.Ok(await arm.ReadAsync(ct));
    }
    catch (Exception ex)
    {
        log.Add("gateway", "Cambio de bloqueo total", null, sw.ElapsedMilliseconds, "error");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/blocks/tools", async (BlockToolsRequest req, ArmClient arm, RequestLog log, CancellationToken ct) =>
{
    var sw = Stopwatch.StartNew();
    var known = Catalog.Tools.Select(t => t.Name).ToHashSet();
    var tools = req.Tools.Where(known.Contains).Distinct().ToArray();
    try
    {
        await arm.SetToolsAsync(tools, ct);
        log.Add("gateway", tools.Length == 0 ? "Sin tools bloqueadas" : $"Tools bloqueadas: {string.Join(", ", tools)}", 200, sw.ElapsedMilliseconds, tools.Length == 0 ? "ok" : "bloqueado");
        return Results.Ok(await arm.ReadAsync(ct));
    }
    catch (Exception ex)
    {
        log.Add("gateway", "Cambio de tools bloqueadas", null, sw.ElapsedMilliseconds, "error");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/limits", async (RateLimitRequest req, ArmClient arm, RequestLog log, CancellationToken ct) =>
{
    var sw = Stopwatch.StartNew();
    var calls = Math.Clamp(req.CallsPerMinute, 0, 600);
    try
    {
        await arm.SetRateLimitAsync(calls, ct);
        log.Add("gateway", calls == 0 ? "Sin límite de llamadas" : $"Límite: {calls} llamadas/min", 200, sw.ElapsedMilliseconds, calls == 0 ? "ok" : "limitado");
        return Results.Ok(await arm.ReadAsync(ct));
    }
    catch (Exception ex)
    {
        log.Add("gateway", "Cambio del límite de llamadas", null, sw.ElapsedMilliseconds, "error");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/audit", async (AuditClient audit, int? minutes, CancellationToken ct) =>
{
    if (!audit.Configured) return Results.Ok(new { Configured = false, Entries = Array.Empty<AuditEntry>() });
    try
    {
        return Results.Ok(new { Configured = true, Entries = await audit.QueryAsync(minutes ?? 60, ct) });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/requests", (RequestLog log) => log.Latest());

// Pregunta al hosted agent y reenvía el stream SSE tal cual; el navegador interpreta los eventos.
app.MapPost("/api/chat", async (ChatRequest req, HttpContext ctx, IHttpClientFactory http, TokenCredential credential, ControlOptions o, RequestLog log) =>
{
    if (o.AgentEndpoint.Length == 0)
    {
        ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await ctx.Response.WriteAsync("Falta Control__AgentEndpoint.", ctx.RequestAborted);
        return;
    }

    var ct = ctx.RequestAborted;
    var token = await credential.GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]), ct);
    var payload = new JsonObject { ["input"] = req.Message, ["stream"] = true, ["store"] = true };
    if (!string.IsNullOrEmpty(req.PreviousResponseId)) payload["previous_response_id"] = req.PreviousResponseId;

    using var message = new HttpRequestMessage(HttpMethod.Post, o.AgentEndpoint)
    {
        Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
    };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

    var sw = Stopwatch.StartNew();
    using var response = await http.CreateClient("agent").SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
    ctx.Response.StatusCode = (int)response.StatusCode;
    ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";

    var seen = new StringBuilder();
    await using var stream = await response.Content.ReadAsStreamAsync(ct);
    var buffer = new byte[4096];
    int read;
    while ((read = await stream.ReadAsync(buffer, ct)) > 0)
    {
        await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
        await ctx.Response.Body.FlushAsync(ct);
        if (seen.Length < 400_000) seen.Append(Encoding.UTF8.GetString(buffer, 0, read));
    }

    var outcome = !response.IsSuccessStatusCode ? "error" : seen.ToString().Contains("AccessBlockedByGateway") ? "bloqueado" : seen.ToString().Contains("rate_limited") ? "limitado" : seen.ToString().Contains("egress_denied") ? "bloqueado" : "ok";
    log.Add("chat", req.Message, (int)response.StatusCode, sw.ElapsedMilliseconds, outcome);
});

// "Probar sin agente": una llamada tools/call directa, por el gateway o saltándoselo (el backend está cerrado: debe dar 401).
app.MapPost("/api/tool-test", async (ToolTestRequest req, IHttpClientFactory http, ControlOptions o, RequestLog log, CancellationToken ct) =>
{
    var direct = req.Mode == "direct";
    var url = direct ? o.McpDirectUrl : o.McpGatewayUrl;
    if (url.Length == 0) return Results.Problem("Falta la URL del MCP en la configuración.", statusCode: StatusCodes.Status503ServiceUnavailable);
    if (!Catalog.Tools.Any(t => t.Name == req.Tool)) return Results.BadRequest("Tool desconocida.");

    var rpc = new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = 1,
        ["method"] = "tools/call",
        ["params"] = new JsonObject { ["name"] = req.Tool, ["arguments"] = req.Arguments?.DeepClone() ?? new JsonObject() },
    };
    using var message = new HttpRequestMessage(HttpMethod.Post, url)
    {
        Content = new StringContent(rpc.ToJsonString(), Encoding.UTF8, "application/json"),
    };
    message.Headers.Accept.ParseAdd("application/json");
    message.Headers.Accept.ParseAdd("text/event-stream");
    message.Headers.Add("x-mcp-key", o.McpKey);

    var sw = Stopwatch.StartNew();
    try
    {
        using var response = await http.CreateClient("mcp").SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var data = body.Split('\n').FirstOrDefault(l => l.StartsWith("data:"))?[5..].Trim() ?? body;
        var status = (int)response.StatusCode;
        var toolError = false;
        try { toolError = JsonNode.Parse(data)?["result"]?["isError"]?.GetValue<bool>() == true; } catch { /* cuerpo no JSON */ }
        var outcome = status is 403 or 401 ? "bloqueado" : status == 429 ? "limitado" : response.IsSuccessStatusCode && !toolError ? "ok" : "error";
        log.Add(direct ? "tool (directo)" : "tool", req.Tool, status, sw.ElapsedMilliseconds, outcome);
        return Results.Ok(new { req.Mode, Status = status, Ms = sw.ElapsedMilliseconds, Outcome = outcome, Body = data.Length > 4000 ? data[..4000] + "…" : data });
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        log.Add(direct ? "tool (directo)" : "tool", req.Tool, null, sw.ElapsedMilliseconds, "error");
        return Results.Ok(new { req.Mode, Status = 0, Ms = sw.ElapsedMilliseconds, Outcome = "error", Body = ex.Message });
    }
});

app.MapFallbackToFile("index.html");

app.Run();
