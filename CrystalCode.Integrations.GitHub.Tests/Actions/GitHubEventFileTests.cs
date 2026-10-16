using CrystalCode.Integrations.GitHub.Actions;

namespace CrystalCode.Integrations.GitHub.Tests.Actions;

public sealed class GitHubEventFileTests
{
    [Fact]
    public async Task ReadAsync_ExistingFile_ReturnsTheText()
    {
        var path = Path.Combine(Path.GetTempPath(), "crystal-github-event-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, """{"number":1}""");
        try
        {
            var text = await GitHubEventFile.ReadAsync(path, CancellationToken.None);

            Assert.Contains("\"number\":1", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_Fails()
    {
        var exception = await Assert.ThrowsAsync<GitHubException>(() =>
            GitHubEventFile.ReadAsync(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".json"), CancellationToken.None));

        Assert.Equal("GitHub event file was not found.", exception.Message);
    }
}
