namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubRequestTests
{
    [Fact]
    public void Constructor_RelativePath_KeepsThePath()
    {
        var request = new GitHubRequest(HttpMethod.Get, "/repos/acme/demo/releases");

        Assert.Equal("/repos/acme/demo/releases", request.Path);
        Assert.Equal("GET /repos/acme/demo/releases", request.ToString());
    }

    [Theory]
    [InlineData("https://evil.test/repos")]
    [InlineData("//evil.test/repos")]
    [InlineData("/repos/../secrets")]
    [InlineData("/repos/acme/demo?access_token=ghp_testtoken")]
    public void Constructor_UnsafePath_IsRejected(string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GitHubRequest(HttpMethod.Get, path));

        Assert.DoesNotContain(TestClients.Token, exception.Message, StringComparison.Ordinal);
    }
}
