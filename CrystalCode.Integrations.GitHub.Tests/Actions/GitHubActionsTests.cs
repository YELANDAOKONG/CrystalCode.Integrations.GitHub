using CrystalCode.Integrations.GitHub.Actions;

namespace CrystalCode.Integrations.GitHub.Tests.Actions;

public sealed class GitHubActionsTests
{
    [Fact]
    public void Read_CompleteEnvironment_UsesTheJobValues()
    {
        var environment = Environment(
            ("GITHUB_API_URL", "https://github.example/api/v3"),
            ("GITHUB_TOKEN", TestClients.Token),
            ("GITHUB_REPOSITORY", "acme/demo"),
            ("GITHUB_EVENT_NAME", "issues"),
            ("GITHUB_EVENT_PATH", "/tmp/event.json"));

        var read = GitHubActions.Read(environment);

        Assert.Equal("https://github.example/api/v3", read.Api.ToString());
        Assert.Equal("acme/demo", read.Repository.ToString());
        Assert.Equal("issues", read.EventName);
        Assert.DoesNotContain(TestClients.Token, read.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingApiUrl_UsesGitHubDotCom()
    {
        var read = GitHubActions.Read(Environment(
            ("GITHUB_TOKEN", TestClients.Token),
            ("GITHUB_REPOSITORY", "acme/demo"),
            ("GITHUB_EVENT_NAME", "issues"),
            ("GITHUB_EVENT_PATH", "/tmp/event.json")));

        Assert.Equal(GitHubLimits.DefaultApi, read.Api.ToString().TrimEnd('/'));
    }

    [Fact]
    public void Read_MissingToken_FailsWithoutASecret()
    {
        var exception = Assert.Throws<GitHubException>(() => GitHubActions.Read(Environment(
            ("GITHUB_REPOSITORY", "acme/demo"),
            ("GITHUB_EVENT_NAME", "issues"),
            ("GITHUB_EVENT_PATH", "/tmp/event.json"))));

        Assert.Equal("GITHUB_TOKEN is missing.", exception.Message);
    }

    private static Dictionary<string, string?> Environment(params (string Name, string Value)[] pairs)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            environment[pair.Name] = pair.Value;
        }

        return environment;
    }
}
