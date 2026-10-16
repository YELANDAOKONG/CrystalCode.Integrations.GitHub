using System.Net;
using System.Text;

namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubClientTests
{
    [Fact]
    public async Task SendAsync_RelativePath_SendsAuthorizationOnTheApiHost()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"ok":true}"""));
        var client = TestClients.Create(handler);

        var response = await client.SendAsync(
            new GitHubRequest(HttpMethod.Post, "/repos/acme/demo/releases", """{"name":"1.0"}"""),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.github.test/repos/acme/demo/releases", request.Uri!.ToString());
        Assert.Equal("Bearer " + TestClients.Token, request.Header("Authorization"));
        Assert.Equal(GitHubLimits.UserAgent, request.Header("User-Agent"));
        Assert.Equal(GitHubLimits.ApiVersion, request.Header("X-GitHub-Api-Version"));
        Assert.Contains(GitHubLimits.DefaultAccept, request.Header("Accept"), StringComparison.Ordinal);
        Assert.Contains("\"name\":\"1.0\"", request.Body, StringComparison.Ordinal);
        Assert.Equal(200, response.StatusCode);
        Assert.DoesNotContain(TestClients.Token, response.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ErrorMentionsToken_OmitsTheDetail()
    {
        var handler = new FakeHttpMessageHandler().Respond(
            TestResponses.Status(HttpStatusCode.Unauthorized, "{\"message\":\"bad " + TestClients.Token + "\"}"));
        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            client.SendAsync(new GitHubRequest(HttpMethod.Get, "/rate_limit"), CancellationToken.None));

        Assert.Equal("GitHub returned HTTP status 401.", exception.Message);
        Assert.DoesNotContain(TestClients.Token, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ErrorMessage_IncludesTheDetail()
    {
        var handler = new FakeHttpMessageHandler().Respond(
            TestResponses.Status(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}"));
        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            client.SendAsync(new GitHubRequest(HttpMethod.Get, "/rate_limit"), CancellationToken.None));

        Assert.Equal("GitHub returned HTTP status 404: Not Found", exception.Message);
    }

    [Fact]
    public async Task SendAsync_ActionsTokenInTheError_OmitsTheDetail()
    {
        const string token = "ghs_actionssecretvalue";
        var handler = new FakeHttpMessageHandler().Respond(
            TestResponses.Status(HttpStatusCode.Unauthorized, "{\"message\":\"bad " + token + "\"}"));
        var client = new GitHubClient(new HttpClient(handler), TestClients.Api, token);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            client.SendAsync(new GitHubRequest(HttpMethod.Get, "/rate_limit"), CancellationToken.None));

        Assert.Equal("GitHub returned HTTP status 401.", exception.Message);
        Assert.DoesNotContain(token, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_ErrorMentionsAnotherTokenPrefix_OmitsTheDetail()
    {
        var handler = new FakeHttpMessageHandler().Respond(
            TestResponses.Status(HttpStatusCode.Unauthorized, "{\"message\":\"bad gho_other\"}"));
        var client = new GitHubClient(new HttpClient(handler), TestClients.Api, "customtokenvalue");

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            client.SendAsync(new GitHubRequest(HttpMethod.Get, "/rate_limit"), CancellationToken.None));

        Assert.Equal("GitHub returned HTTP status 401.", exception.Message);
        Assert.DoesNotContain("gho_other", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_JsonOverTheLimit_Fails()
    {
        var body = new string('a', GitHubLimits.MaximumResponseBytes + 8);
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Text(body, "application/json"));
        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            client.SendAsync(new GitHubRequest(HttpMethod.Get, "/rate_limit"), CancellationToken.None));

        Assert.Equal("GitHub response exceeded 1 MiB.", exception.Message);
    }

    [Fact]
    public async Task SendAsync_TextOverTheLimit_Truncates()
    {
        var body = new string('a', GitHubLimits.MaximumResponseBytes + 8);
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Text(body, "text/plain"));
        var client = TestClients.Create(handler);

        var response = await client.SendAsync(
            new GitHubRequest(HttpMethod.Get, "/repos/acme/demo/contents/README.md"),
            CancellationToken.None);

        Assert.True(response.Truncated);
        Assert.EndsWith(GitHubLimits.TruncationNote, response.Body, StringComparison.Ordinal);
        Assert.Equal(GitHubLimits.MaximumResponseBytes, Encoding.UTF8.GetByteCount(response.Body[..^GitHubLimits.TruncationNote.Length]));
    }

    [Fact]
    public void Constructor_HttpApi_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new GitHubClient(new HttpClient(new FakeHttpMessageHandler()), new Uri("http://api.github.test"), TestClients.Token));

        Assert.StartsWith("GitHub API URL must use HTTPS.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentsPerPage_FitsInOneResponse()
    {
        Assert.InRange(GitHubLimits.CommentsPerPage, 1, 100);
        Assert.True(GitHubLimits.CommentsPerPage * GitHubLimits.MaximumCommentJsonBytes <= GitHubLimits.MaximumResponseBytes);
    }
}
