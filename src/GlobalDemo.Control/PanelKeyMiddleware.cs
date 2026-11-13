using System.Security.Cryptography;
using System.Text;

namespace GlobalDemo.Control;

/// <summary>
/// Protege /api con una clave en la cabecera x-panel-key, pensada para el panel publicado en Azure (la Canvas App la envía desde
/// el conector personalizado). Solo se activa si existe Control__PanelKey; sin ella no hace nada y el panel local se comporta como siempre.
/// </summary>
public static class PanelKeyMiddleware
{
    public const string Header = "x-panel-key";

    public static void UsePanelKey(this WebApplication app)
    {
        var key = app.Services.GetRequiredService<ControlOptions>().PanelKey;
        if (string.IsNullOrEmpty(key)) return;

        var expected = Encoding.UTF8.GetBytes(key);
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                var given = Encoding.UTF8.GetBytes(ctx.Request.Headers[Header].ToString());
                if (!CryptographicOperations.FixedTimeEquals(given, expected))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsync($"Falta la cabecera {Header} o es incorrecta.", ctx.RequestAborted);
                    return;
                }
            }
            await next();
        });
    }
}
