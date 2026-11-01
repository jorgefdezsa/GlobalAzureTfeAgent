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

builder.Build().Run();
