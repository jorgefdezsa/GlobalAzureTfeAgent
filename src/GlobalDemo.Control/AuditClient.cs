using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;

namespace GlobalDemo.Control;

/// <summary>Lee la auditoría del gateway: las trazas mcp-audit que la policy envía a Application Insights.</summary>
public sealed class AuditClient(IHttpClientFactory http, TokenCredential credential, ControlOptions options)
{
    private const string Kql = """
        customEvents
        | where timestamp > ago({0}m) and name == "mcp-audit"
        | extend d = customDimensions
        | project timestamp, client = tostring(d["client"]), tool = tostring(d["tool"]), args = tostring(d["args"]),
                  outcome = tostring(d["outcome"]), status = tostring(d["status"])
        | order by timestamp desc
        | take 60
        """;

    public bool Configured => options.AppInsights.Length > 0;

    public async Task<AuditEntry[]> QueryAsync(int minutes, CancellationToken ct)
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        var url = $"https://management.azure.com/subscriptions/{options.SubscriptionId}/resourceGroups/{options.ResourceGroup}" +
                  $"/providers/Microsoft.Insights/components/{options.AppInsights}/query?api-version=2018-04-20";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(new JsonObject { ["query"] = string.Format(Kql, Math.Clamp(minutes, 1, 1440)) }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await http.CreateClient("arm").SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Application Insights {(int)response.StatusCode}: {body}");

        var table = JsonNode.Parse(body)?["tables"]?[0];
        var columns = table?["columns"]?.AsArray().Select(c => c?["name"]?.GetValue<string>() ?? "").ToList() ?? [];
        string Cell(JsonArray row, string name) => row[columns.IndexOf(name)]?.ToString() ?? "";
        return [.. table?["rows"]?.AsArray().Select(r =>
        {
            var row = r!.AsArray();
            return new AuditEntry(DateTimeOffset.Parse(Cell(row, "timestamp")), Cell(row, "client"), Cell(row, "tool"), Cell(row, "args"), Cell(row, "outcome"),
                int.TryParse(Cell(row, "status"), out var s) ? s : null);
        }) ?? []];
    }
}
