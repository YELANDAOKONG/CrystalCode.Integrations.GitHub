using CrystalCode.Integrations.GitHub.Tools;

namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubCodeSnapshotTests
{
    [Fact]
    public async Task ResolveCommitAsync_PullRequestUsesEventHeadSha()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var handler = new FakeHttpMessageHandler();
        var client = TestClients.Create(handler);
        var path = WriteEvent($"{{\"pull_request\":{{\"head\":{{\"sha\":\"{commit}\"}}}}}}");
        try
        {
            var result = await GitHubCodeSnapshot.ResolveCommitAsync(
                client, GitHubRepository.Parse("acme/demo"), Environment(path, "pull_request"), CancellationToken.None);

            Assert.Equal(commit, result);
            Assert.Empty(handler.Requests);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommitAsync_IssueUsesConfiguredRef()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json($"[{{\"sha\":\"{commit}\"}}]"));
        var client = TestClients.Create(handler);
        var path = WriteEvent("{\"issue\":{\"number\":12}}");
        try
        {
            var result = await GitHubCodeSnapshot.ResolveCommitAsync(
                client,
                GitHubRepository.Parse("acme/demo"),
                Environment(path, "issues", "feature/docs"),
                CancellationToken.None);

            Assert.Equal(commit, result);
            Assert.Contains("sha=feature%2Fdocs", handler.Requests[0].Uri!.Query, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommitAsync_IssueDefaultsToRepositoryDefaultBranch()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json("{\"default_branch\":\"trunk\"}"))
            .Respond(TestResponses.Json($"[{{\"sha\":\"{commit}\"}}]"));
        var client = TestClients.Create(handler);
        var path = WriteEvent("{\"issue\":{\"number\":12}}");
        try
        {
            var result = await GitHubCodeSnapshot.ResolveCommitAsync(
                client, GitHubRepository.Parse("acme/demo"), Environment(path, "issues"), CancellationToken.None);

            Assert.Equal(commit, result);
            Assert.EndsWith("/repos/acme/demo", handler.Requests[0].Uri!.AbsolutePath, StringComparison.Ordinal);
            Assert.Contains("sha=trunk", handler.Requests[1].Uri!.Query, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCommitAsync_PullRequestCommentLoadsCurrentHead()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json($"{{\"head\":{{\"sha\":\"{commit}\"}}}}"));
        var client = TestClients.Create(handler);
        var path = WriteEvent("{\"issue\":{\"number\":12,\"pull_request\":{\"url\":\"https://api.github.com/pulls/12\"}}}");
        try
        {
            var result = await GitHubCodeSnapshot.ResolveCommitAsync(
                client, GitHubRepository.Parse("acme/demo"), Environment(path, "issue_comment"), CancellationToken.None);

            Assert.Equal(commit, result);
            Assert.EndsWith("/repos/acme/demo/pulls/12", handler.Requests[0].Uri!.AbsolutePath, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteEvent(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), "github-event-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, contents);
        return path;
    }

    private static Dictionary<string, string?> Environment(string eventPath, string eventName, string? codeRef = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GITHUB_EVENT_PATH"] = eventPath,
            ["GITHUB_EVENT_NAME"] = eventName
        };
        if (codeRef is not null)
        {
            values["GITHUB_CODE_REF"] = codeRef;
        }

        return values;
    }
}
