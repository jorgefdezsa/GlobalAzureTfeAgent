namespace GlobalDemo.Aviation;

public sealed class AviationStackException(string code, string message)
    : Exception($"AviationStack {code}: {message}")
{
    public string Code { get; } = code;
}
