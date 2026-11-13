using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;

namespace GlobalDemo.Control;

public sealed record ToolCallInfo(string Name, string Arguments, string State, string Detail);

public sealed record UsageInfo(int Input, int Cached, int Output);

/// <summary>State: ok | blocked | limited | egress | error | pending (no llegó el resultado de la tool).</summary>
public sealed record ChatSyncResponse(string? ResponseId, string Text, long Ms, ToolCallInfo[] ToolCalls, UsageInfo? Usage, string? Error);

/// <summary>
/// POST /api/chat-sync: lo mismo que /api/chat pero sin streaming, para clientes que no leen SSE (la Canvas App, vía conector personalizado).
/// Devuelve la respuesta completa con las tools llamadas y su estado ya clasificado. /api/chat no se toca.
/// </summary>
public static class ChatSyncEndpoint
{
    // Misma clasificación que el panel React (web/src/App.tsx) hace al leer el stream.
    private static readonly Regex ToolFailed = new("\"status\":\"(gateway_error|tool_error|error|http_error)\"", RegexOptions.Compiled);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(100); // los conectores cortan en torno a los 2 minutos

    public static void MapChatSync(this WebApplication app) => app.MapPost("/api/chat-sync", HandleAsync);

    private static async Task<IResult> HandleAsync(ChatRequest req, IHttpClientFactory http, TokenCredential credential, ControlOptions o, RequestLog log, CancellationToken aborted)
    {
        if (o.AgentEndpoint.Length == 0) return Results.Problem("Falta Control__AgentEndpoint.", statusCode: StatusCodes.Status503ServiceUnavailable);
        if (string.IsNullOrWhiteSpace(req.Message)) return Results.BadRequest("El mensaje está vacío.");

        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        cts.CancelAfter(Limit);
        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]), cts.Token);
            var payload = new JsonObject { ["input"] = req.Message, ["stream"] = false, ["store"] = true };
            if (!string.IsNullOrEmpty(req.PreviousResponseId)) payload["previous_response_id"] = req.PreviousResponseId;

            using var message = new HttpRequestMessage(HttpMethod.Post, o.AgentEndpoint)
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using var response = await http.CreateClient("agent").SendAsync(message, cts.Token);
            var body = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                log.Add("chat", req.Message, (int)response.StatusCode, sw.ElapsedMilliseconds, "error");
                return Results.Ok(Failure(sw, $"El agente respondió HTTP {(int)response.StatusCode}: {Cut(body, 300)}"));
            }

            var result = Parse(body, sw.ElapsedMilliseconds);
            var outcome = result.Error is not null ? "error"
                : body.Contains("AccessBlockedByGateway") ? "bloqueado"
                : body.Contains("rate_limited") ? "limitado"
                : body.Contains("egress_denied") ? "bloqueado" : "ok";
            log.Add("chat", req.Message, (int)response.StatusCode, sw.ElapsedMilliseconds, outcome);
            return Results.Ok(result);
        }
        catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
        {
            log.Add("chat", req.Message, null, sw.ElapsedMilliseconds, "error");
            return Results.Ok(Failure(sw, $"El agente tardó más de {Limit.TotalSeconds:0} s en responder."));
        }
        catch (Exception ex) when (ex is HttpRequestException or Azure.Identity.AuthenticationFailedException)
        {
            log.Add("chat", req.Message, null, sw.ElapsedMilliseconds, "error");
            return Results.Ok(Failure(sw, ex.Message));
        }
    }

    internal static ChatSyncResponse Parse(string body, long ms)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(body); }
        catch (System.Text.Json.JsonException) { return new ChatSyncResponse(null, "", ms, [], null, $"Respuesta del agente no válida: {Cut(body, 300)}"); }

        var id = root?["id"]?.GetValue<string>();
        var errorMessage = root?["error"]?["message"]?.GetValue<string>();
        if (errorMessage is null && root?["status"]?.GetValue<string>() == "failed") errorMessage = "La respuesta falló";

        var text = new StringBuilder();
        var calls = new List<(string Name, string Args, string? CallId, string State, string Detail)>();

        foreach (var item in root?["output"]?.AsArray() ?? [])
        {
            switch (item?["type"]?.GetValue<string>())
            {
                case "message":
                    foreach (var part in item["content"]?.AsArray() ?? [])
                        if (part?["type"]?.GetValue<string>() == "output_text") text.Append(part["text"]?.GetValue<string>());
                    break;
                case "function_call":
                    calls.Add((item["name"]?.GetValue<string>() ?? "", item["arguments"]?.ToString() ?? "", item["call_id"]?.GetValue<string>(), "pending", ""));
                    break;
                case "function_call_output":
                    var output = item["output"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : item["output"]?.ToJsonString() ?? "";
                    var callId = item["call_id"]?.GetValue<string>();
                    var at = callId is null ? -1 : calls.FindIndex(c => c.CallId == callId);
                    if (at < 0) at = calls.FindLastIndex(c => c.State == "pending");
                    if (at >= 0)
                    {
                        var (state, detail) = Classify(output);
                        calls[at] = calls[at] with { State = state, Detail = detail };
                    }
                    break;
            }
        }

        var usage = root?["usage"];
        UsageInfo? u = usage is null ? null : new UsageInfo(Int(usage["input_tokens"]), Int(usage["input_tokens_details"]?["cached_tokens"]), Int(usage["output_tokens"]));
        return new ChatSyncResponse(id, text.ToString(), ms, [.. calls.Select(c => new ToolCallInfo(c.Name, c.Args, c.State, c.Detail))], u, errorMessage);
    }

    /// <summary>Estado y detalle de una tool a partir de su salida (mismos textos que el panel React).</summary>
    internal static (string State, string Detail) Classify(string output)
    {
        var blocked = output.Contains("\"status\":\"blocked\"");
        var limited = output.Contains("\"status\":\"rate_limited\"");
        var egress = output.Contains("\"status\":\"egress_denied\"");
        var failed = !blocked && !limited && !egress && ToolFailed.IsMatch(output);
        if (blocked) return ("blocked", "403 AccessBlockedByGateway · la policy corta la llamada y no llega al MCP");
        if (limited) return ("limited", "429 · el rate limit del gateway corta la llamada y no llega al MCP");
        if (egress) return ("egress", "403 · el proxy de egress de Foundry deniega el destino: el agente solo puede salir por el AI Gateway");
        if (failed) return ("error", Cut(output, 160));
        return ("ok", $"200 · el MCP responde ({output.Length} caracteres)");
    }

    private static ChatSyncResponse Failure(Stopwatch sw, string error) => new(null, "", sw.ElapsedMilliseconds, [], null, error);

    private static int Int(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;
}
