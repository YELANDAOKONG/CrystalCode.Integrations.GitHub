using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.PullRequests;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Reads one pull request diff in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubPullDiffTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_pull_diff",
        "Read one pull request diff in GITHUB_REPOSITORY. output writes the diff to a workspace path instead of returning it.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "number": { "type": "integer", "minimum": 1 },
            "output": { "type": "string" }
          },
          "required": ["number"]
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
        var diff = await GitHubPullRequests.GetDiffAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            cancellationToken).ConfigureAwait(false);
        var output = GitHubArguments.Optional(arguments, "output");
        if (output is null)
        {
            return diff.Body;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new GitHubException("Missing output.");
        }

        try
        {
            await File.WriteAllTextAsync(output, diff.Body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GitHubException("Could not write the GitHub response.");
        }

        return "Wrote the pull request diff.";
    }
}
