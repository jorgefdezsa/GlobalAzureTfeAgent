using System.Diagnostics;

/// <summary>
/// Lee el entorno por defecto de azd (.azure/&lt;entorno&gt;/.env), de donde el panel toma el agente, el gateway y las claves.
/// Si no hay entorno, devuelve vacío y el panel arranca sin configurar (mismo aviso que sin run-control.sh).
/// </summary>
internal sealed class AzdEnvironment
{
    private readonly Dictionary<string, string> _values;

    private AzdEnvironment(Dictionary<string, string> values) => _values = values;

    public string this[string key] => _values.GetValueOrDefault(key, "");

    public bool Found => _values.Count > 0;

    public static AzdEnvironment Load(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var config = Path.Combine(dir.FullName, ".azure", "config.json");
            if (!File.Exists(config)) continue;

            var name = System.Text.Json.JsonDocument.Parse(File.ReadAllText(config)).RootElement.GetProperty("defaultEnvironment").GetString();
            var env = Path.Combine(dir.FullName, ".azure", name ?? "", ".env");
            if (!File.Exists(env)) break;

            var values = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(env))
            {
                var eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith('#')) continue;
                values[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"');
            }
            return new AzdEnvironment(values);
        }
        return new AzdEnvironment([]);
    }

    /// <summary>Compila el frontend del panel si todavía no existe wwwroot (necesita Node).</summary>
    public static void EnsureControlWeb(string controlProjectDirectory)
    {
        if (File.Exists(Path.Combine(controlProjectDirectory, "wwwroot", "index.html"))) return;
        var web = Path.Combine(controlProjectDirectory, "web");
        Console.WriteLine("Compilando el frontend del panel (npm install && npm run build)…");
        using var process = Process.Start(new ProcessStartInfo("sh", ["-c", "npm install && npm run build"]) { WorkingDirectory = web })!;
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("No se pudo compilar el frontend del panel.");
    }
}
