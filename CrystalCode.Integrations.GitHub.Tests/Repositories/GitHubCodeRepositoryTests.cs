using CrystalCode.Integrations.GitHub.Repositories;

namespace CrystalCode.Integrations.GitHub.Tests.Repositories;

public sealed class GitHubCodeRepositoryTests
{
    private static readonly GitHubRepository Repository = GitHubRepository.Parse("acme/demo");

    [Fact]
    public async Task ResolveCommitAsync_UsesEncodedRefAndReturnsCommitSha()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var handler = new FakeHttpMessageHandler()
            .Respond(TestResponses.Json($"[{{\"sha\":\"{commit}\"}}]"));
        var client = TestClients.Create(handler);

        var result = await GitHubCodeRepository.ResolveCommitAsync(
            client, Repository, "feature/one", CancellationToken.None);

        Assert.Equal(commit, result);
        Assert.Contains("sha=feature%2Fone", handler.Requests[0].Uri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDirectoryAsync_UsesCommitAndObjectMediaType()
    {
        var handler = new FakeHttpMessageHandler().Respond(TestResponses.Json("{\"entries\":[]}"));
        var client = TestClients.Create(handler);

        await GitHubCodeRepository.GetDirectoryAsync(
            client,
            Repository,
            "0123456789abcdef0123456789abcdef01234567",
            "src/My Folder",
            CancellationToken.None);

        Assert.Equal("application/vnd.github.object+json", handler.Requests[0].Header("Accept"));
        Assert.EndsWith("/contents/src/My%20Folder?ref=0123456789abcdef0123456789abcdef01234567",
            handler.Requests[0].Uri!.PathAndQuery, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData(".crystal/config.json")]
    [InlineData("src/AGENTS.md")]
    [InlineData("CLAUDE.md")]
    [InlineData("instructions.md")]
    public void ValidatePath_RejectsTraversalAndInstructionFiles(string path) =>
        Assert.Throws<GitHubException>(() => GitHubCodeRepository.ValidatePath(path, allowEmpty: false));

    [Fact]
    public void ValidatePath_AllowsEmptyRootDirectory() =>
        GitHubCodeRepository.ValidatePath(string.Empty, allowEmpty: true);
}
