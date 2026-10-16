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
        Assert.Contains("CrystalCode.Integrations.GitHub.Command request", error.ToString(), StringComparison.Ordinal);
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

    [Fact]
    public async Task RunAsync_IssueGet_DoesNotRequireTheEventFile()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"number":4}"""));
        var output = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["issue", "get", "--number", "4"],
            ApiEnvironment(),
            () => new HttpClient(handler),
            output,
            new StringWriter(),
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("\"number\":4", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("https://api.github.test/repos/acme/demo/issues/4", handler.Requests[0].Uri!.ToString());
    }

    [Fact]
    public async Task RunAsync_IssueComment_AcceptsInlineBody()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"id":1}"""));

        var code = await GitHubCommand.RunAsync(
            ["issue", "comment", "--number", "7", "--body", "Please reproduce."],
            ApiEnvironment(),
            () => new HttpClient(handler),
            new StringWriter(),
            new StringWriter(),
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("\"body\":\"Please reproduce.\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Request_AcceptsMethodAndPathFlags()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"ok":true}"""));

        var code = await GitHubCommand.RunAsync(
            ["request", "--method", "GET", "--path", "/repos/acme/demo/releases"],
            ApiEnvironment(),
            () => new HttpClient(handler),
            new StringWriter(),
            new StringWriter(),
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Equal("https://api.github.test/repos/acme/demo/releases", handler.Requests[0].Uri!.ToString());
    }

    [Fact]
    public async Task RunAsync_BothBodyForms_IsRejected()
    {
        var error = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["issue", "comment", "--number", "7", "--body", "Hello", "--body-file", "comment.txt"],
            ApiEnvironment(),
            () => new HttpClient(new FakeHttpMessageHandler()),
            new StringWriter(),
            error,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("Use either --body or --body-file." + System.Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task RunAsync_EventShow_StillRequiresTheEventName()
    {
        var error = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["event", "show"],
            ApiEnvironment(),
            () => new HttpClient(new FakeHttpMessageHandler()),
            new StringWriter(),
            error,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("GITHUB_EVENT_NAME is missing." + System.Environment.NewLine, error.ToString());
    }

    [Fact]
    public async Task RunAsync_IncompleteCommentList_FailsWithoutPrintingIt()
    {
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json("[{\"id\":1}]", "<https://evil.test/comments?page=2>; rel=\"next\""));
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await GitHubCommand.RunAsync(
            ["issue", "comments", "7"],
            Environment(),
            () => new HttpClient(handler),
            output,
            error,
            CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("GitHub comment list is incomplete." + System.Environment.NewLine, error.ToString());
        Assert.Equal(string.Empty, output.ToString());
        Assert.Single(handler.Requests);
    }

    private static Dictionary<string, string?> Environment() => new(StringComparer.Ordinal)
    {
        ["GITHUB_TOKEN"] = TestClients.Token,
        ["GITHUB_API_URL"] = TestClients.Api.ToString(),
        ["GITHUB_REPOSITORY"] = "acme/demo",
        ["GITHUB_EVENT_NAME"] = "issues",
        ["GITHUB_EVENT_PATH"] = "event.json"
    };

    private static Dictionary<string, string?> ApiEnvironment() => new(StringComparer.Ordinal)
    {
        ["GITHUB_TOKEN"] = TestClients.Token,
        ["GITHUB_API_URL"] = TestClients.Api.ToString(),
        ["GITHUB_REPOSITORY"] = "acme/demo"
    };
}
