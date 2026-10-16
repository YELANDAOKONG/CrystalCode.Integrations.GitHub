namespace CrystalCode.Integrations.GitHub.Tests;

internal sealed record CapturedRequest(
    string Method,
    Uri? Uri,
    IReadOnlyDictionary<string, string> Headers,
    string Body)
{
    public string? Header(string name) =>
        Headers.TryGetValue(name, out var value) ? value : null;
}
