namespace CrystalCode.Integrations.GitHub.Tests;

public sealed class GitHubRepositoryTests
{
    [Fact]
    public void Parse_OwnerAndName_SplitsOnce()
    {
        var repository = GitHubRepository.Parse("acme/demo.net");

        Assert.Equal("acme", repository.Owner);
        Assert.Equal("demo.net", repository.Name);
        Assert.Equal("/repos/acme/demo.net", repository.ApiPath);
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("acme/demo/extra")]
    [InlineData("acme/demo name")]
    public void Parse_NotOwnerAndName_IsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => GitHubRepository.Parse(value));
    }
}
