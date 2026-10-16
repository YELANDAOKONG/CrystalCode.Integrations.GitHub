using CrystalCode.Integrations.GitHub.PullRequests;

namespace CrystalCode.Integrations.GitHub.Tests.PullRequests;

public sealed class GitHubPullRequestsTests
{
    private static readonly GitHubRepository Repository = GitHubRepository.Parse("acme/demo");

    [Fact]
    public async Task GetDiffAsync_Number_RequestsTheDiffMediaType()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Text("diff --git a/A b/A\n", GitHubLimits.DiffAccept));
        var client = TestClients.Create(handler);

        var response = await GitHubPullRequests.GetDiffAsync(client, Repository, 4, CancellationToken.None);

        Assert.Equal(GitHubLimits.DiffAccept, handler.Requests[0].Header("Accept"));
        Assert.Equal("https://api.github.test/repos/acme/demo/pulls/4", handler.Requests[0].Uri!.ToString());
        Assert.Contains("diff --git", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateReviewAsync_Comment_SendsTheEvent()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("""{"id":9}"""));
        var client = TestClients.Create(handler);

        await GitHubPullRequests.CreateReviewAsync(
            client,
            Repository,
            4,
            "One issue.",
            PullRequestReviewEvent.RequestChanges,
            "0123456789abcdef",
            CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal("POST", request.Method);
        Assert.EndsWith("/pulls/4/reviews", request.Uri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"event\":\"REQUEST_CHANGES\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"commit_id\":\"0123456789abcdef\"", request.Body, StringComparison.Ordinal);
    }
}
