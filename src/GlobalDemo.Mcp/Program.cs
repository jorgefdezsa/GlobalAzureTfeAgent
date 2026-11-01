using System.Security.Cryptography;
using System.Text;
using GlobalDemo.Aviation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddAviation();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();

// Si hay Mcp:ApiKey (p. ej. publicado en Container Apps), /mcp exige la cabecera Ocp-Apim-Subscription-Key,
// la misma que usará API Management después. Sin clave configurada (local) no se pide nada.
var apiKey = app.Configuration["Mcp:ApiKey"];
if (!string.IsNullOrEmpty(apiKey))
{
    var expected = Encoding.UTF8.GetBytes(apiKey);
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/mcp"))
        {
            var sent = Encoding.UTF8.GetBytes(ctx.Request.Headers["Ocp-Apim-Subscription-Key"].ToString());
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
