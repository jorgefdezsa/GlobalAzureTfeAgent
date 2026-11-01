using Microsoft.Extensions.DependencyInjection;

namespace GlobalDemo.Aviation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAviation(this IServiceCollection services)
    {
        services.AddOptions<AviationStackOptions>()
            .BindConfiguration(AviationStackOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddHybridCache();
        services.AddHttpClient<AviationStackClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AviationStackOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl);
        })
        // Sustituye la resiliencia por defecto de ServiceDefaults (10 s por intento): AviationStack
        // puede tardar más en consultas amplias.
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<AviationService>();
        return services;
    }
}
