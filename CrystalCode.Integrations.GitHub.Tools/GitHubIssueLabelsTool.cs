using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Issues;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Adds or replaces labels on one issue in <c>GITHUB_REPOSITORY</c>.</summary>
public sealed class GitHubIssueLabelsTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_issue_labels",
        "Add labels or replace the label set on one issue in GITHUB_REPOSITORY. set with an empty list clears the labels.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "number": { "type": "integer", "minimum": 1 },
            "mode": { "type": "string", "enum": ["add", "set"] },
            "labels": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["number", "mode", "labels"]
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
        var number = GitHubArguments.Number(arguments);
        var labels = GitHubArguments.Strings(arguments, "labels");
        var response = GitHubArguments.Required(arguments, "mode") switch
        {
            "add" => await GitHubIssues.AddLabelsAsync(client, repository, number, labels, cancellationToken)
                .ConfigureAwait(false),
            "set" => await GitHubIssues.SetLabelsAsync(client, repository, number, labels, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new GitHubException("Label mode must be add or set.")
        };
        return GitHubTool.Text(response);
    }
}
