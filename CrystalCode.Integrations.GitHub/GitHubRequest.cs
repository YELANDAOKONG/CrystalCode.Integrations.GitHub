namespace CrystalCode.Integrations.GitHub;

/// <summary>
/// One relative GitHub API call. The path stays on the API host so the token
/// is not sent anywhere else. Other endpoints use this type; they do not need
/// a new method.
/// </summary>
public sealed record GitHubRequest
{
    public GitHubRequest(HttpMethod method, string path, string? jsonBody = null, string? accept = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsRelativePath(path))
        {
            throw new ArgumentException("GitHub path must be one relative API path.", nameof(path));
        }

        if (accept is not null && !IsHeaderValue(accept))
        {
            throw new ArgumentException("GitHub accept value must be one header value.", nameof(accept));
        }

        if (jsonBody is not null && jsonBody.Length > GitHubLimits.MaximumRequestBytes)
        {
            throw new ArgumentException("GitHub request body exceeded 1 MiB.", nameof(jsonBody));
        }

        Method = method;
        Path = path;
        JsonBody = jsonBody;
        Accept = accept;
    }

    public HttpMethod Method { get; }

    public string Path { get; }

    public string? JsonBody { get; }

    public string? Accept { get; }

    public override string ToString() => $"{Method} {Path}";

    private static bool IsRelativePath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains("..", StringComparison.Ordinal)
            || path.Contains('@')
            || path.Contains('\\')
            || path.Contains(' ')
            || path.Contains('\r')
            || path.Contains('\n'))
        {
            return false;
        }

        if (path.Contains("access_token", StringComparison.OrdinalIgnoreCase)
            || path.Contains("client_secret", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsHeaderValue(string value) =>
        value.Length > 0
        && value.Length <= GitHubLimits.MaximumHeaderCharacters
        && !value.Contains('\r')
        && !value.Contains('\n');
}
