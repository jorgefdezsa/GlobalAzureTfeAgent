var builder = DistributedApplication.CreateBuilder(args);

// Parámetros en user-secrets del AppHost:
//   dotnet user-secrets set Parameters:aviationstack-key <clave>
//   dotnet user-secrets set Parameters:openai-endpoint https://<recurso>.openai.azure.com/
//   dotnet user-secrets set Parameters:openai-deployment <nombre-del-despliegue>
var accessKey = builder.AddParameter("aviationstack-key", secret: true);
var openAiEndpoint = builder.AddParameter("openai-endpoint");
var openAiDeployment = builder.AddParameter("openai-deployment");

var api = builder.AddProject<Projects.GlobalDemo_Api>("api")
    .WithEnvironment("AviationStack__AccessKey", accessKey)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var mcp = builder.AddProject<Projects.GlobalDemo_Mcp>("mcp")
    .WithEnvironment("AviationStack__AccessKey", accessKey)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Parte 2: apuntar Agent__McpUrl al AI Gateway en lugar de al servicio "mcp".
builder.AddProject<Projects.GlobalDemo_Agent>("agent")
    .WithReference(mcp)
    .WaitFor(mcp)
    .WithEnvironment("Agent__McpUrl", "http://mcp/mcp")
    .WithEnvironment("Agent__OpenAiEndpoint", openAiEndpoint)
    .WithEnvironment("Agent__Deployment", openAiDeployment)
    .WithExternalHttpEndpoints();

// Panel de control (React): habla con el agente de Foundry y el AI Gateway con la identidad de `az login`, no con el MCP local.
// La configuración sale del entorno de azd (.azure/<entorno>/.env); solo escucha en localhost.
var azd = AzdEnvironment.Load(builder.AppHostDirectory);
if (!azd.Found) Console.WriteLine("Aviso: no hay entorno de azd; el panel arrancará sin configurar (azd env select).");
AzdEnvironment.EnsureControlWeb(Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "GlobalDemo.Control")));

builder.AddProject<Projects.GlobalDemo_Control>("control")
    .WithHttpEndpoint(port: 5200, name: "http")
    .WithEnvironment("Control__SubscriptionId", azd["AZURE_SUBSCRIPTION_ID"])
    .WithEnvironment("Control__ResourceGroup", azd["AZURE_RESOURCE_GROUP"])
    .WithEnvironment("Control__AgentEndpoint", azd["AGENT_ASISTENTE_VUELOS_RESPONSES_ENDPOINT"])
    .WithEnvironment("Control__McpGatewayUrl", azd["MCP_URL"])
    .WithEnvironment("Control__McpKey", azd["MCP_PANEL_KEY"] is { Length: > 0 } panelKey ? panelKey : azd["MCP_SUBSCRIPTION_KEY"])
    .WithEnvironment("Control__McpDirectUrl", azd["MCP_DIRECT_URL"])
    .WithEnvironment("Control__AppInsights", azd["AUDIT_APPINSIGHTS_NAME"])
    .WithEnvironment("Control__PriceInputPerMillion", azd["MODEL_PRICE_INPUT"])
    .WithEnvironment("Control__PriceCachedPerMillion", azd["MODEL_PRICE_CACHED"])
    .WithEnvironment("Control__PriceOutputPerMillion", azd["MODEL_PRICE_OUTPUT"]);

builder.Build().Run();
