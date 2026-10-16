using CrystalCode.Integrations.GitHub.Issues;

namespace CrystalCode.Integrations.GitHub.Tests.Issues;

public sealed class GitHubIssuesTests
{
    private static readonly GitHubRepository Repository = GitHubRepository.Parse("acme/demo");

    [Fact]
    public async Task GetAsync_Number_UsesTheIssuePath()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"number":7}"""));
        var client = TestClients.Create(handler);

        await GitHubIssues.GetAsync(client, Repository, 7, CancellationToken.None);

        Assert.Equal("https://api.github.test/repos/acme/demo/issues/7", handler.Requests[0].Uri!.ToString());
    }

    [Fact]
    public async Task CreateCommentAsync_Body_SendsJson()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"id":1}"""));
        var client = TestClients.Create(handler);

        await GitHubIssues.CreateCommentAsync(client, Repository, 7, "Please reproduce.", CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.github.test/repos/acme/demo/issues/7/comments", request.Uri!.ToString());
        Assert.Contains("\"body\":\"Please reproduce.\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateCommentAsync_EmptyBody_IsRejected()
    {
        var client = TestClients.Create(new FakeHttpMessageHandler());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            GitHubIssues.CreateCommentAsync(client, Repository, 7, " ", CancellationToken.None));

        Assert.StartsWith("GitHub comment body is required.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetLabelsAsync_Labels_ReplacesTheList()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("[]"));
        var client = TestClients.Create(handler);

        await GitHubIssues.SetLabelsAsync(client, Repository, 7, ["bug"], CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal("PUT", request.Method);
        Assert.EndsWith("/issues/7/labels", request.Uri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"labels\":[\"bug\"]", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListCommentsAsync_NextOnTheSameHost_MergesPages()
    {
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json("[{\"id\":1}]", "<https://api.github.test/repos/acme/demo/issues/7/comments?page=2>; rel=\"next\""))
            .Respond(TestResponses.Json("[{\"id\":2}]"));
        var client = TestClients.Create(handler);

        var response = await GitHubIssues.ListCommentsAsync(client, Repository, 7, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("per_page=" + GitHubLimits.CommentsPerPage, handler.Requests[0].Uri!.Query, StringComparison.Ordinal);
        Assert.Contains("page=2", handler.Requests[1].Uri!.Query, StringComparison.Ordinal);
        Assert.Contains("\"id\":1", response.Body, StringComparison.Ordinal);
        Assert.Contains("\"id\":2", response.Body, StringComparison.Ordinal);
        Assert.False(response.Truncated);
    }

    [Fact]
    public async Task ListCommentsAsync_NextOnAnotherHost_FailsWithoutFollowing()
    {
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json("[{\"id\":1}]", "<https://evil.test/comments?page=2>; rel=\"next\""));
        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            GitHubIssues.ListCommentsAsync(client, Repository, 7, CancellationToken.None));

        Assert.Single(handler.Requests);
        Assert.Equal("GitHub comment list is incomplete.", exception.Message);
        Assert.DoesNotContain("evil.test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListCommentsAsync_MoreCommentsThanTheCap_FailsBeforeTheNextPage()
    {
        var handler = new FakeHttpMessageHandler();
        for (var page = 1; page <= GitHubLimits.MaximumCommentPages; page++)
        {
            handler.Respond(TestResponses.Json(
                $"[{{\"id\":{page}}}]",
                $"<https://api.github.test/repos/acme/demo/issues/7/comments?page={page + 1}>; rel=\"next\""));
        }

        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            GitHubIssues.ListCommentsAsync(client, Repository, 7, CancellationToken.None));

        Assert.Equal(GitHubLimits.MaximumCommentPages, handler.Requests.Count);
        Assert.Equal($"GitHub comment list exceeded {GitHubLimits.MaximumComments} comments.", exception.Message);
    }

    [Fact]
    public async Task ListCommentsAsync_CombinedListOverTheBudget_Fails()
    {
        var body = "[" + new string(' ', GitHubLimits.MaximumResponseBytes - 2) + "]";
        var handler = new FakeHttpMessageHandler();
        for (var page = 1; page <= 6; page++)
        {
            handler.Respond(TestResponses.Json(
                body,
                $"<https://api.github.test/repos/acme/demo/issues/7/comments?page={page + 1}>; rel=\"next\""));
        }

        var client = TestClients.Create(handler);

        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            GitHubIssues.ListCommentsAsync(client, Repository, 7, CancellationToken.None));

        Assert.Equal(6, handler.Requests.Count);
        Assert.Equal("GitHub comment list exceeded 5 MiB.", exception.Message);
    }
}
