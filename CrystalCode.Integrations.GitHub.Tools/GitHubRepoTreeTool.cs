using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Repositories;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Lists one repository directory at a pinned commit.</summary>
public sealed class GitHubRepoTreeTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_repo_tree",
        "List files and directories at one repository path. Omit commit to select the configured issue branch or current pull request head; reuse the returned full commit SHA for subsequent lookups.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string" },
            "commit": { "type": "string" }
          }
        }
        """);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeApiAsync(call, RunAsync, cancellationToken);

    private static async Task<string> RunAsync(
        GitHubClient client,
        GitHubRepository repository,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var path = GitHubArguments.Optional(arguments, "path") ?? string.Empty;
        GitHubCodeRepository.ValidatePath(path, allowEmpty: true);
        var commit = GitHubArguments.Optional(arguments, "commit");
        if (commit is null)
        {
            commit = await GitHubCodeSnapshot.ResolveCommitAsync(
                client, repository, GitHubTool.ReadEnvironment(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            GitHubCodeRepository.ValidateCommit(commit);
        }

        var response = await GitHubCodeRepository.GetDirectoryAsync(
            client, repository, commit, path, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(response.Body);
        var entries = document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("entries", out var objectEntries)
                ? objectEntries
                : document.RootElement;
        if (entries.ValueKind != JsonValueKind.Array)
        {
            throw new GitHubException("GitHub did not return a repository directory.");
        }

        var lines = new List<string> { $"Commit: {commit}", $"Directory: {(path.Length == 0 ? "." : path)}" };
        foreach (var entry in entries.EnumerateArray().OrderBy(ReadName, StringComparer.Ordinal))
        {
            if (!entry.TryGetProperty("name", out var nameValue)
                || nameValue.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameValue.GetString()!;
            if (GitHubCodeRepository.IsInstructionPath(name))
            {
                continue;
            }

            var type = entry.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String
                ? typeValue.GetString() : "unknown";
            var size = entry.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var length)
                ? $" ({length} bytes)" : string.Empty;
            lines.Add($"{type}: {name}{(type == "file" ? size : string.Empty)}");
        }

        return string.Join('\n', lines);
    }

    private static string ReadName(JsonElement element) =>
        element.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()! : string.Empty;
}
