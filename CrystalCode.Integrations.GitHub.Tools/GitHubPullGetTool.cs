using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.PullRequests;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Reads one pull request in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubPullGetTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_pull_get",
        "Read one pull request in GITHUB_REPOSITORY.",
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
        var response = await GitHubPullRequests.GetAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }
}
