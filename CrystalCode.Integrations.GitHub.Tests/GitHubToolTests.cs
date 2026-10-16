using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Tools;

namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubToolTests
{
    [Fact]
    public void Definitions_UseDistinctNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in Tools())
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Definition.Description));
            Assert.Equal(JsonValueKind.Object, tool.Definition.InputSchema.ValueKind);
            Assert.True(names.Add(tool.Definition.Name));
        }

        Assert.Equal(9, names.Count);
        Assert.Contains("github_request", names);
        Assert.Contains("github_event_show", names);
    }

    [Fact]
    public async Task InvokeAsync_MissingNumber_ReturnsFailure()
    {
        GitHubTool.ReadEnvironment = static () => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GITHUB_TOKEN"] = "test-token",
            ["GITHUB_REPOSITORY"] = "acme/demo"
        };
        try
        {
            var output = await new GitHubIssueGetTool().InvokeAsync(
                new ToolCall("call-1", "github_issue_get", "{}"),
                CancellationToken.None);

            Assert.Equal(ToolResultStatus.Failure, output.Status);
            Assert.Contains("numbers start at 1", output.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("test-token", output.Text, StringComparison.Ordinal);
        }
        finally
        {
            GitHubTool.UseProcessEnvironment();
        }
    }

    [Fact]
    public async Task InvokeAsync_MissingToken_ReturnsFailure()
    {
        GitHubTool.ReadEnvironment = static () => new Dictionary<string, string?>(StringComparer.Ordinal);
        try
        {
            var output = await new GitHubIssueGetTool().InvokeAsync(
                new ToolCall("call-1", "github_issue_get", """{"number":1}"""),
                CancellationToken.None);

            Assert.Equal(ToolResultStatus.Failure, output.Status);
            Assert.Contains("GITHUB_TOKEN", output.Text, StringComparison.Ordinal);
        }
        finally
        {
            GitHubTool.UseProcessEnvironment();
        }
    }

    private static IEnumerable<ITool> Tools()
    {
        yield return new GitHubRequestTool();
        yield return new GitHubIssueGetTool();
        yield return new GitHubIssueCommentsTool();
        yield return new GitHubIssueCommentTool();
        yield return new GitHubIssueLabelsTool();
        yield return new GitHubPullGetTool();
        yield return new GitHubPullDiffTool();
        yield return new GitHubPullReviewTool();
        yield return new GitHubEventShowTool();
    }
}
