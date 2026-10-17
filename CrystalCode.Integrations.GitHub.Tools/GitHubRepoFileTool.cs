using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Repositories;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Fetches one repository text file into the isolated workspace.</summary>
public sealed class GitHubRepoFileTool : ITool
{
    private const string SnapshotDirectory = ".github-source";
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_repo_file",
        "Fetch one text file from a pinned repository commit into .github-source/ in the workspace. Use local read, grep, and search tools on the fetched snapshot.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "path": { "type": "string" },
            "commit": { "type": "string" }
          },
          "required": ["path", "commit"]
        }
        """);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeApiAsync(
            call,
            (client, repository, arguments, token) => RunAsync(
                client, repository, arguments, Environment.CurrentDirectory, token),
            cancellationToken);

    private static async Task<string> RunAsync(
        GitHubClient client,
        GitHubRepository repository,
        JsonElement arguments,
        string workspace,
        CancellationToken cancellationToken)
    {
        var path = GitHubArguments.Required(arguments, "path");
        GitHubCodeRepository.ValidatePath(path, allowEmpty: false);
        var commit = GitHubArguments.Required(arguments, "commit");
        GitHubCodeRepository.ValidateCommit(commit);

        var metadata = await GitHubCodeRepository.GetFileMetadataAsync(
            client, repository, commit, path, cancellationToken).ConfigureAwait(false);
        using (var document = JsonDocument.Parse(metadata.Body))
        {
            if (!document.RootElement.TryGetProperty("type", out var type)
                || type.GetString() != "file"
                || document.RootElement.TryGetProperty("submodule_git_url", out _))
            {
                throw new GitHubException("Only regular repository files can be fetched.");
            }

            if (document.RootElement.TryGetProperty("size", out var size)
                && size.TryGetInt64(out var length)
                && length > GitHubCodeRepository.MaximumFileBytes)
            {
                throw new GitHubException("Repository file exceeded the 1 MiB read limit.");
            }
        }

        var content = await GitHubCodeRepository.GetFileContentAsync(
            client, repository, commit, path, cancellationToken).ConfigureAwait(false);
        if (content.Truncated || content.Body.Contains('\0'))
        {
            throw new GitHubException("Repository file is too large or is not a text file.");
        }

        var destination = SnapshotPath(workspace, path);
        var directory = Path.GetDirectoryName(destination)!;
        CreateSafeDirectory(workspace, directory);
        if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
        {
            throw new GitHubException("Repository snapshot path must not be a symbolic link.");
        }

        await File.WriteAllTextAsync(destination, content.Body, cancellationToken).ConfigureAwait(false);
        return $"Fetched {path} from commit {commit} to {Path.GetRelativePath(workspace, destination)}.";
    }

    private static string SnapshotPath(string workspace, string path)
    {
        var root = Path.GetFullPath(Path.Combine(workspace, SnapshotDirectory));
        var destination = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new GitHubException("Repository snapshot path is not valid.");
        }

        return destination;
    }

    private static void CreateSafeDirectory(string workspace, string directory)
    {
        var root = Path.GetFullPath(workspace);
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, directory).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                Directory.CreateDirectory(current);
            }

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new GitHubException("Repository snapshot directories must not be symbolic links.");
            }
        }
    }
}
