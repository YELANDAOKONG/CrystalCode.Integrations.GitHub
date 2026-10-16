namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubCommandTests
{
    [Fact]
    public async Task RunAsync_Request_WritesTheResponseBody()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"ok":true}"""));
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["request", "GET", "/repos/acme/demo/releases"],
            Environment(),
            () => new HttpClient(handler),
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("\"ok\":true", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("https://api.github.test/repos/acme/demo/releases", handler.Requests[0].Uri!.ToString());
    }

    [Fact]
    public async Task RunAsync_UnknownCommand_PrintsUsageWithoutTheToken()
    {
        var error = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["review-pr"],
            Environment(),
            () => new HttpClient(new FakeHttpMessageHandler()),
            new StringWriter(),
            error,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("request <METHOD> <PATH>", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TestClients.Token, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingToken_FailsClosed()
    {
        var error = new StringWriter();
        var environment = Environment();
        environment.Remove("GITHUB_TOKEN");

        var code = await GitHubCommand.RunAsync(
            ["issue", "get", "1"],
            environment,
            () => new HttpClient(new FakeHttpMessageHandler()),
            new StringWriter(),
            error,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("GITHUB_TOKEN is missing." + System.Environment.NewLine, error.ToString());
    }

    private static Dictionary<string, string?> Environment() => new(StringComparer.Ordinal)
    {
        ["GITHUB_TOKEN"] = TestClients.Token,
        ["GITHUB_API_URL"] = TestClients.Api.ToString(),
        ["GITHUB_REPOSITORY"] = "acme/demo",
        ["GITHUB_EVENT_NAME"] = "issues",
        ["GITHUB_EVENT_PATH"] = "event.json"
    };
}
