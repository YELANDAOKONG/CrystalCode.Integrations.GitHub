using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Issues;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Creates one issue comment in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubIssueCommentTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_issue_comment",
        "Create one issue comment in GITHUB_REPOSITORY.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "number": { "type": "integer", "minimum": 1 },
            "body": { "type": "string" }
          },
          "required": ["number", "body"]
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
        var response = await GitHubIssues.CreateCommentAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            GitHubArguments.Required(arguments, "body"),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }
}
