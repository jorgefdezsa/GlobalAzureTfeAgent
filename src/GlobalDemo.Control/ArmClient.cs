using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;

namespace GlobalDemo.Control;

/// <summary>Lee y escribe los interruptores del AI Gateway (named values de API Management) con la identidad de az login.</summary>
public sealed class ArmClient(IHttpClientFactory http, TokenCredential credential, ControlOptions options)
{
    private const string Api = "api-version=2024-06-01-preview";
    private const string AllId = "mcp-blocked";
    private const string ToolsId = "mcp-blocked-tools";
    private const string RateId = "mcp-rate-limit";

    public async Task<BlockState> ReadAsync(CancellationToken ct)
    {
        var all = ReadValueAsync(AllId, ct);
        var tools = ReadValueAsync(ToolsId, ct);
        var rate = ReadValueAsync(RateId, ct);
        var toolList = (await tools).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("none", StringComparison.OrdinalIgnoreCase)).ToArray();
        var calls = int.TryParse(await rate, out var n) && n > 0 ? n : 0;
        return new BlockState((await all).Equals("true", StringComparison.OrdinalIgnoreCase), toolList, calls);
    }

    public Task SetAllAsync(bool blocked, CancellationToken ct) => WriteValueAsync(AllId, blocked ? "true" : "false", ct);

    public Task SetRateLimitAsync(int callsPerMinute, CancellationToken ct) => WriteValueAsync(RateId, Math.Max(0, callsPerMinute).ToString(), ct);

    public Task SetToolsAsync(IEnumerable<string> tools, CancellationToken ct)
    {
        var list = string.Join(',', tools);
        return WriteValueAsync(ToolsId, list.Length == 0 ? "none" : list, ct);
    }

    /// <summary>Lee la RAI policy del agente (guardrail de contenido + egress). Found = false si no existe: entonces el agente no filtra nada.</summary>
    public async Task<GuardrailState> ReadGuardrailAsync(CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com/subscriptions/{options.SubscriptionId}/resourceGroups/{options.ResourceGroup}" +
            $"/providers/Microsoft.CognitiveServices/accounts/{options.FoundryAccount}/raiPolicies/{options.GuardrailPolicy}?api-version=2026-05-15-preview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await http.CreateClient("arm").SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new GuardrailState(false, false, "", []);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Foundry {(int)response.StatusCode} al leer la policy: {body}");
        var props = JsonNode.Parse(body)?["properties"];
        var jailbreak = props?["contentFilters"]?.AsArray().Any(f =>
            f?["name"]?.GetValue<string>() == "Jailbreak" && f["source"]?.GetValue<string>() == "Prompt" &&
            f["enabled"]?.GetValue<bool>() == true && f["blocking"]?.GetValue<bool>() == true) ?? false;
        var egress = props?["egressPolicy"];
        var hosts = egress?["rules"]?.AsArray()
            .Where(r => r?["action"]?["actionType"]?.GetValue<string>() == "Allow")
            .Select(r => r?["match"]?["host"]?.GetValue<string>() ?? "*").ToArray() ?? [];
        return new GuardrailState(true, jailbreak, egress?["mode"]?.GetValue<string>() ?? "", hosts);
    }

    private string Url(string id) =>
        $"https://management.azure.com/subscriptions/{options.SubscriptionId}/resourceGroups/{options.ResourceGroup}" +
        $"/providers/Microsoft.ApiManagement/service/{options.Apim}/namedValues/{id}?{Api}";

    private async Task<string> ReadValueAsync(string id, CancellationToken ct)
    {
        using var request = await NewRequestAsync(HttpMethod.Get, id, ct);
        using var response = await http.CreateClient("arm").SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"API Management {(int)response.StatusCode} al leer {id}: {body}");
        return JsonNode.Parse(body)?["properties"]?["value"]?.GetValue<string>() ?? "";
    }

    private async Task WriteValueAsync(string id, string value, CancellationToken ct)
    {
        using var request = await NewRequestAsync(HttpMethod.Patch, id, ct);
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
        request.Content = new StringContent(new JsonObject { ["properties"] = new JsonObject { ["value"] = value } }.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.CreateClient("arm").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"API Management {(int)response.StatusCode} al escribir {id}: {await response.Content.ReadAsStringAsync(ct)}");
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string id, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        var request = new HttpRequestMessage(method, Url(id));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }
}
