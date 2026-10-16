namespace CrystalCode.Integrations.GitHub;

/// <summary>The status, body, and headers of one GitHub response.</summary>
public sealed record GitHubResponse(
    int StatusCode,
    string Body,
    bool Truncated,
    IReadOnlyDictionary<string, string> Headers)
{
    public override string ToString() => $"HTTP {StatusCode}";
}
