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

// Si hay Mcp:ApiKey (p. ej. publicado en Container Apps), /mcp exige la clave de backend en x-backend-key. Solo la conoce
// el AI Gateway, que la inyecta; la clave que usan los clientes (x-mcp-key) la valida el gateway y no sirve aquí, así que
// llamar a este servidor directamente no se salta el bloqueo. Sin clave configurada (local) no se pide nada.
var apiKey = app.Configuration["Mcp:ApiKey"];
if (!string.IsNullOrEmpty(apiKey))
{
    var expected = Encoding.UTF8.GetBytes(apiKey);
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/mcp"))
        {
            var sent = Encoding.UTF8.GetBytes(ctx.Request.Headers["x-backend-key"].ToString());
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
