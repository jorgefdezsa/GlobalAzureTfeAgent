using System.Security.Cryptography;
using System.Text;
using GlobalDemo.Aviation;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddAviation();
builder.Services
    .AddMcpServer()
    // Sin estado: el agente abre una sesión MCP por llamada y detrás del gateway puede haber varias réplicas.
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();

// Si hay Mcp:ApiKey (p. ej. publicado en Container Apps), /mcp exige la clave en x-mcp-key (la que usa la conexión de la
// tool en Foundry) o en Ocp-Apim-Subscription-Key. Sin clave configurada (local) no se pide nada.
var apiKey = app.Configuration["Mcp:ApiKey"];
if (!string.IsNullOrEmpty(apiKey))
{
    var expected = Encoding.UTF8.GetBytes(apiKey);
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/mcp"))
        {
            var header = ctx.Request.Headers["x-mcp-key"].ToString();
            if (header.Length == 0) header = ctx.Request.Headers["Ocp-Apim-Subscription-Key"].ToString();
            var sent = Encoding.UTF8.GetBytes(header);
            if (!CryptographicOperations.FixedTimeEquals(sent, expected))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await next();
    });
}

app.MapMcp("/mcp");

app.Run();
