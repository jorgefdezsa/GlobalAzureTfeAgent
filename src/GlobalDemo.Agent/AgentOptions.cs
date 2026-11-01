using System.ComponentModel.DataAnnotations;

namespace GlobalDemo.Agent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Endpoint de Azure OpenAI / Foundry, p. ej. https://mi-recurso.openai.azure.com/</summary>
    [Required]
    public string OpenAiEndpoint { get; set; } = string.Empty;

    [Required]
    public string Deployment { get; set; } = "gpt-4.1";

    /// <summary>URL del servidor MCP. En Aspire es la dirección del servicio "mcp"; en la parte 2, la del AI Gateway.</summary>
    [Required]
    public string McpUrl { get; set; } = "http://mcp/mcp";

    /// <summary>Cabecera opcional para el gateway (p. ej. Ocp-Apim-Subscription-Key).</summary>
    public string? McpSubscriptionKey { get; set; }
}
