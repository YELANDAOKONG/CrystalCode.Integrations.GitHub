namespace CrystalCode.Integrations.GitHub;

/// <summary>
/// A GitHub failure whose message is safe to print. It never includes the token.
/// </summary>
public sealed class GitHubException : Exception
{
    public GitHubException(string message)
        : base(message)
    {
    }
}
