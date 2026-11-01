using System.ComponentModel.DataAnnotations;

namespace GlobalDemo.Aviation;

public sealed class AviationStackOptions
{
    public const string SectionName = "AviationStack";

    // El plan gratuito de AviationStack solo admite HTTP: si la clave es gratuita, configurar http://api.aviationstack.com/v1/.
    [Required]
    public string BaseUrl { get; set; } = "https://api.aviationstack.com/v1/";

    [Required]
    public string AccessKey { get; set; } = string.Empty;
}
