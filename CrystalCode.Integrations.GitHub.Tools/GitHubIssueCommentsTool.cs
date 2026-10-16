using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Issues;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Lists comments on one issue in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubIssueCommentsTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_issue_comments",
        "List comments on one issue in GITHUB_REPOSITORY.",
        GitHubIssueGetTool.NumberSchema);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeApiAsync(call, RunAsync, cancellationToken);

    private static async Task<string> RunAsync(
        GitHubClient client,
        GitHubRepository repository,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var response = await GitHubIssues.ListCommentsAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }
}
