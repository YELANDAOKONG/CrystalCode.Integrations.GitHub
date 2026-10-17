using System.Text.Json;

namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubToolManifestTests
{
    [Fact]
    public void ToolsJson_DeclaresADotnetRunner()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tools.json")));
        var root = document.RootElement;

        Assert.Equal("dotnet", root.GetProperty("runner").GetString());
        Assert.Equal("CrystalCode.Integrations.GitHub.Tools.dll", root.GetProperty("assembly").GetString());
        Assert.False(root.TryGetProperty("command", out _));

        var tools = root.GetProperty("tools");
        Assert.Equal(JsonValueKind.Object, tools.ValueKind);
        Assert.Equal("work", tools.GetProperty("github_pull_review").GetProperty("catalogs")[0].GetString());
        Assert.Equal(1, tools.GetProperty("github_pull_review").GetProperty("catalogs").GetArrayLength());
        Assert.Equal("work", tools.GetProperty("github_request").GetProperty("catalogs")[0].GetString());
        Assert.Equal("work", tools.GetProperty("github_issue_comment").GetProperty("catalogs")[0].GetString());
        Assert.Equal("work", tools.GetProperty("github_issue_labels").GetProperty("catalogs")[0].GetString());
        Assert.Equal("output", tools.GetProperty("github_pull_diff").GetProperty("pathArguments")[0].GetString());
        Assert.Equal(2, tools.GetProperty("github_repo_tree").GetProperty("catalogs").GetArrayLength());
        Assert.Equal(2, tools.GetProperty("github_repo_file").GetProperty("catalogs").GetArrayLength());
        Assert.False(tools.TryGetProperty("github_issue_get", out _));
        Assert.False(tools.TryGetProperty("github_event_show", out _));
    }
}
