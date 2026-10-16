using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Issues;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Reads one issue in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubIssueGetTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_issue_get",
        "Read one issue in GITHUB_REPOSITORY.",
        NumberSchema);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeApiAsync(call, RunAsync, cancellationToken);

    private static async Task<string> RunAsync(
        GitHubClient client,
        GitHubRepository repository,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var response = await GitHubIssues.GetAsync(
            client,
            repository,
            GitHubArguments.Number(arguments),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }

    internal const string NumberSchema =
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "number": { "type": "integer", "minimum": 1 }
          },
          "required": ["number"]
        }
        """;
}
