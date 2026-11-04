using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace GlobalDemo.AgentHosted;

/// <summary>
/// TOOL SOLO PARA LA DEMO del control de egress. Hace un GET a la URL que le pasen desde el propio contenedor del agente,
/// por eso su tráfico pasa por el proxy de egress de Foundry. Con la policy en Enforced, cualquier host que no sea el AI Gateway
/// recibe un 403 del proxy ("aunque un desarrollador meta un atajo, el agente solo puede salir por el gateway").
/// Es deliberadamente un "atajo" inseguro; no la dejes en un agente real.
/// </summary>
internal sealed class ConsultarUrlTool
{
    // Cliente propio y sin cabeceras del MCP: esta tool no debe llevar la clave del gateway a otros hosts.
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public AITool Create() => AIFunctionFactory.Create(ConsultarUrl, "consultar_url",
        "Hace una petición GET a una URL de internet y devuelve el código de estado y el comienzo de la respuesta. " +
        "Úsala solo cuando el usuario pida consultar una URL concreta.");

    private async Task<string> ConsultarUrl([Description("URL completa, p. ej. https://api.aviationstack.com/v1/flights")] string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return Result("error", url, "URL no válida: debe empezar por http:// o https://.");
        try
        {
            using var response = await _http.GetAsync(uri, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var text = body.Length > 300 ? body[..300] + "…" : body;
            // El proxy de egress responde 403 cuando la policy deniega el destino; se distingue del bloqueo del gateway
            // (status "blocked" en las tools de vuelos) para que el modelo y el panel expliquen el motivo correcto.
            return response.StatusCode == HttpStatusCode.Forbidden
                ? Result("egress_denied", url, $"HTTP 403: la policy de egress del agente no permite salir a {uri.Host}. {text}")
                : Result(response.IsSuccessStatusCode ? "ok" : "http_error", url, $"HTTP {(int)response.StatusCode}: {text}");
        }
        catch (HttpRequestException ex)
        {
            return Result(ex.StatusCode == HttpStatusCode.Forbidden ? "egress_denied" : "error", url, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return Result("error", url, "La petición ha tardado demasiado (timeout).");
        }
    }

    // status: "ok" | "http_error" | "egress_denied" | "error". Las instrucciones del agente, el panel y la consola dependen de estos valores.
    private static string Result(string status, string url, string message) =>
        JsonSerializer.Serialize(new { status, tool = "consultar_url", url, message });
}
