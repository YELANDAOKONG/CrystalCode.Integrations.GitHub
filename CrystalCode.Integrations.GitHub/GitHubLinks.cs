namespace CrystalCode.Integrations.GitHub;

/// <summary>
/// Reads a GitHub Link header. A next URL on another host is rejected so the
/// token is not sent there.
/// </summary>
internal static class GitHubLinks
{
    public static bool TryFollow(
        Uri api,
        IReadOnlyDictionary<string, string> headers,
        out string? path,
        out bool rejected)
    {
        path = null;
        rejected = false;
        if (!headers.TryGetValue("Link", out var link))
        {
            return false;
        }

        foreach (var part in link.Split(','))
        {
            var pieces = part.Split(';');
            if (pieces.Length < 2 || !IsNext(pieces[1]))
            {
                continue;
            }

            var urlText = pieces[0].Trim();
            if (urlText.StartsWith('<') && urlText.EndsWith('>'))
            {
                urlText = urlText[1..^1];
            }

            if (!Uri.TryCreate(urlText, UriKind.Absolute, out var next))
            {
                rejected = true;
                return false;
            }

            if (!TryRelative(api, next, out path))
            {
                rejected = true;
                path = null;
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsNext(string relation) =>
        relation.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase)
        || relation.Contains("rel=next", StringComparison.OrdinalIgnoreCase);

    private static bool TryRelative(Uri api, Uri next, out string? path)
    {
        path = null;
        if (!string.Equals(api.Scheme, next.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(api.Authority, next.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = api.AbsolutePath == "/" ? string.Empty : api.AbsolutePath.TrimEnd('/');
        if (!next.AbsolutePath.StartsWith(prefix + "/", StringComparison.Ordinal))
        {
            return false;
        }

        path = next.AbsolutePath[prefix.Length..] + next.Query;
        return path.StartsWith('/');
    }
}
