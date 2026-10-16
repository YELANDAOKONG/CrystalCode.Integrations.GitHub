using CrystalCode.Integrations.GitHub.Actions;

namespace CrystalCode.Integrations.GitHub.Tests.Actions;

public sealed class GitHubEventNumbersTests
{
    [Fact]
    public void Read_Issue_ReturnsTheIssueNumber()
    {
        var numbers = GitHubEventNumbers.Read("""{"action":"opened","issue":{"number":4}}""");

        Assert.Equal(4, numbers.IssueNumber);
        Assert.Null(numbers.PullRequestNumber);
    }

    [Fact]
    public void Read_IssueCommentOnPullRequest_ReturnsBothNumbers()
    {
        var numbers = GitHubEventNumbers.Read(
            """{"action":"created","issue":{"number":7,"pull_request":{"url":"https://api.github.com/repos/acme/demo/pulls/7"}}}""");

        Assert.Equal(7, numbers.IssueNumber);
        Assert.Equal(7, numbers.PullRequestNumber);
    }

    [Fact]
    public void Read_PullRequest_ReturnsBothNumbers()
    {
        var numbers = GitHubEventNumbers.Read("""{"action":"opened","number":3,"pull_request":{"number":3}}""");

        Assert.Equal(3, numbers.IssueNumber);
        Assert.Equal(3, numbers.PullRequestNumber);
    }
}
