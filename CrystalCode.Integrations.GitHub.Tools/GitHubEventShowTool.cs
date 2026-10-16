using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Actions;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Prints the GitHub Actions event payload.</summary>
public sealed class GitHubEventShowTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_event_show",
        "Read the GitHub Actions event payload. GITHUB_EVENT_NAME and GITHUB_EVENT_PATH are required.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {}
        }
        """);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeEventAsync(
            call,
            (context, token) => GitHubEventFile.ReadAsync(context.EventPath, token),
            cancellationToken);
}
