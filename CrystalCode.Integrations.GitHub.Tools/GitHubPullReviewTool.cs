using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.PullRequests;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Creates one pull request review in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubPullReviewTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_pull_review",
        "Create one pull request review in GITHUB_REPOSITORY.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "number": { "type": "integer", "minimum": 1 },
            "body": { "type": "string" },
            "event": { "type": "string", "enum": ["comment", "approve", "request-changes"] },
            "commit": { "type": "string" }
          },
          "required": ["number", "body", "event"]
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
        var response = await GitHubPullRequests.CreateReviewAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            GitHubArguments.Required(arguments, "body"),
            ReviewEvent(GitHubArguments.Required(arguments, "event")),
            GitHubArguments.Optional(arguments, "commit"),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }

    private static PullRequestReviewEvent ReviewEvent(string value) => value switch
    {
        "comment" => PullRequestReviewEvent.Comment,
        "approve" => PullRequestReviewEvent.Approve,
        "request-changes" => PullRequestReviewEvent.RequestChanges,
        _ => throw new GitHubException("Review event must be comment, approve, or request-changes.")
    };
}
