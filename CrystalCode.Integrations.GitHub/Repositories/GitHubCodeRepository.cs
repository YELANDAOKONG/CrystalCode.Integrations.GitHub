using System.Text.Json;

namespace CrystalCode.Integrations.GitHub.Repositories;

/// <summary>Reads repository paths and file content at an immutable commit.</summary>
public static class GitHubCodeRepository
{
    public const int MaximumFileBytes = 1024 * 1024;
    public const string DirectoryAccept = "application/vnd.github.object+json";
    public const string RawContentAccept = "application/vnd.github.raw+json";

    public static async Task<string> ResolveCommitAsync(
        GitHubClient client,
        GitHubRepository repository,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var request = new GitHubRequest(
            HttpMethod.Get,
            $"{repository.ApiPath}/commits?sha={Uri.EscapeDataString(reference)}&per_page=1");
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() == 0
                || !document.RootElement[0].TryGetProperty("sha", out var sha)
                || sha.ValueKind != JsonValueKind.String
                || !IsCommit(sha.GetString()))
            {
                throw new GitHubException("GitHub did not resolve the repository reference to a commit.");
            }

            return sha.GetString()!;
        }
        catch (JsonException)
        {
            throw new GitHubException("GitHub returned an invalid commit response.");
        }
    }

    public static Task<GitHubResponse> GetDirectoryAsync(
        GitHubClient client,
        GitHubRepository repository,
        string commit,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(repository);
        ValidateCommit(commit);
        ValidatePath(path, allowEmpty: true);
        var endpoint = ContentsPath(repository, path);
        var request = new GitHubRequest(
            HttpMethod.Get,
            $"{endpoint}?ref={Uri.EscapeDataString(commit)}",
            accept: DirectoryAccept);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<GitHubResponse> GetFileMetadataAsync(
        GitHubClient client,
        GitHubRepository repository,
        string commit,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(repository);
        ValidateCommit(commit);
        ValidatePath(path, allowEmpty: false);
        var request = new GitHubRequest(
            HttpMethod.Get,
            $"{ContentsPath(repository, path)}?ref={Uri.EscapeDataString(commit)}",
            accept: DirectoryAccept);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<GitHubResponse> GetFileContentAsync(
        GitHubClient client,
        GitHubRepository repository,
        string commit,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(repository);
        ValidateCommit(commit);
        ValidatePath(path, allowEmpty: false);
        var request = new GitHubRequest(
            HttpMethod.Get,
            $"{ContentsPath(repository, path)}?ref={Uri.EscapeDataString(commit)}",
            accept: RawContentAccept);
        return client.SendAsync(request, cancellationToken);
    }

    public static void ValidateCommit(string commit)
    {
        if (!IsCommit(commit))
        {
            throw new GitHubException("A full Git commit SHA is required.");
        }
    }

    public static void ValidatePath(string path, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (allowEmpty && path.Length == 0)
            {
                return;
            }

            throw new GitHubException("Repository path is required.");
        }

        if (path.StartsWith('/') || path.Contains('\\') || path.Contains(':') || path.Contains('\0'))
        {
            throw new GitHubException("Repository path is not valid.");
        }

        var segments = path.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." || IsInstructionPath(segment)))
        {
            throw new GitHubException("Repository path is not available for reading.");
        }
    }

    public static bool IsInstructionPath(string segment) =>
        segment.Equals(".crystal", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("CONTEXT.md", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("instructions.md", StringComparison.OrdinalIgnoreCase)
        || segment.Equals(".crystal.md", StringComparison.OrdinalIgnoreCase);

    private static string ContentsPath(GitHubRepository repository, string path)
    {
        if (path.Length == 0)
        {
            return $"{repository.ApiPath}/contents";
        }

        var escaped = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return $"{repository.ApiPath}/contents/{escaped}";
    }

    private static bool IsCommit(string? commit) =>
        commit is { Length: 40 or 64 }
        && commit.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
}
