using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Actions;

/// <summary>Reads the event payload file GitHub Actions provides.</summary>
public static class GitHubEventFile
{
    public static async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new GitHubException("GitHub event file was not found.");
        }

        if (info.Length > GitHubLimits.MaximumEventBytes)
        {
            throw new GitHubException("GitHub event file exceeded 1 MiB.");
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GitHubException("GitHub event file could not be read.");
        }
    }
}
