using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

// Cliente de la demo: llama al hosted agent por el protocolo Responses con stream y pinta en vivo cada tool call
// y su resultado, para que se vea el bloqueo del gateway en cuanto ocurre.
//   dotnet run --project src/GlobalDemo.DemoClient            (conversación interactiva)
//   dotnet run --project src/GlobalDemo.DemoClient -- "¿Estado del vuelo RYR963?"
// Endpoint: DEMO_AGENT_ENDPOINT o, si no está, el de `azd env get-value AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT`.
string endpoint = Environment.GetEnvironmentVariable("DEMO_AGENT_ENDPOINT") ?? AzdValue("AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT")
    ?? throw new InvalidOperationException("Define DEMO_AGENT_ENDPOINT o ejecuta desde un directorio con entorno de azd.");

AccessToken token = await new DefaultAzureCredential().GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]));
using HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
string? previousResponseId = null;

if (args.Length > 0)
{
    await AskAsync(string.Join(' ', args));
    return;
}

Console.WriteLine("Asistente de vuelos (línea vacía para salir)");
while (true)
{
    Console.Write("\n> ");
    if (Console.ReadLine() is not { Length: > 0 } prompt) break;
    await AskAsync(prompt);
}

async Task AskAsync(string prompt)
{
    using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
    {
        // previous_response_id solo se envía a partir de la segunda pregunta: el servidor rechaza null.
        Content = JsonContent.Create(previousResponseId is null
            ? new Dictionary<string, object> { ["input"] = prompt, ["stream"] = true, ["store"] = true }
            : new Dictionary<string, object> { ["input"] = prompt, ["stream"] = true, ["store"] = true, ["previous_response_id"] = previousResponseId }),
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

    using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    if (!response.IsSuccessStatusCode)
    {
        Paint(ConsoleColor.Red, $"[HTTP {(int)response.StatusCode}] {await response.Content.ReadAsStringAsync()}");
        return;
    }

    using StreamReader reader = new(await response.Content.ReadAsStreamAsync());
    while (await reader.ReadLineAsync() is { } line)
    {
        if (!line.StartsWith("data: ", StringComparison.Ordinal) || line == "data: [DONE]") continue;
        using JsonDocument doc = JsonDocument.Parse(line[6..]);
        JsonElement e = doc.RootElement;
        switch (e.GetProperty("type").GetString())
        {
            case "response.output_text.delta":
                Console.Write(e.GetProperty("delta").GetString());
                break;
            case "response.output_item.done":
                ShowItem(e.GetProperty("item"));
                break;
            case "response.completed":
                previousResponseId = e.GetProperty("response").GetProperty("id").GetString();
                Console.WriteLine();
                break;
            case "response.failed":
                Paint(ConsoleColor.Red, $"\n[RESPONSE FAILED] {e.GetRawText()}");
                break;
        }
    }
}

static void ShowItem(JsonElement item)
{
    switch (item.GetProperty("type").GetString())
    {
        case "function_call":
            Paint(ConsoleColor.Cyan, $"\n[TOOL] {item.GetProperty("name").GetString()} {item.GetProperty("arguments").GetString()}");
            break;
        case "function_call_output":
            string output = item.GetProperty("output").ValueKind == JsonValueKind.String
                ? item.GetProperty("output").GetString() ?? ""
                : item.GetProperty("output").GetRawText();
            bool blocked = output.Contains("\"status\":\"blocked\"", StringComparison.Ordinal);
            string shown = output.Length > 240 ? output[..240] + "…" : output;
            bool limited = output.Contains("\"status\":\"rate_limited\"", StringComparison.Ordinal);
            Paint(blocked ? ConsoleColor.Red : limited ? ConsoleColor.Yellow : ConsoleColor.Green,
                $"{(blocked ? "[BLOCKED]" : limited ? "[LIMITED]" : "[RESULT]")} {shown}");
            break;
    }
}

static void Paint(ConsoleColor color, string text)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}

static string? AzdValue(string name)
{
    try
    {
        using Process p = Process.Start(new ProcessStartInfo("azd", $"env get-value {name}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string value = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit();
        return p.ExitCode == 0 && value.Length > 0 ? value : null;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return null;
    }
}
