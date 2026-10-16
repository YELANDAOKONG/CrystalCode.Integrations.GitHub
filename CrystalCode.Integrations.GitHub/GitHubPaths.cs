namespace CrystalCode.Integrations.GitHub;

internal static class GitHubPaths
{
    public static string Issue(GitHubRepository repository, int number)
    {
        ArgumentNullException.ThrowIfNull(repository);
        RequireNumber(number);
        return $"{repository.ApiPath}/issues/{number}";
    }

    public static string Pull(GitHubRepository repository, int number)
    {
        ArgumentNullException.ThrowIfNull(repository);
        RequireNumber(number);
        return $"{repository.ApiPath}/pulls/{number}";
    }

    public static void RequireNumber(int number)
    {
        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                "GitHub issue and pull request numbers start at 1.");
        }
    }
}
