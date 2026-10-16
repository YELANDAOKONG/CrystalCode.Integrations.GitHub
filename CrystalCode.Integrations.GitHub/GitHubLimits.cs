namespace CrystalCode.Integrations.GitHub;

/// <summary>Bounds for one GitHub request and the text around it.</summary>
internal static class GitHubLimits
{
    public const int MaximumResponseBytes = 1024 * 1024;
    public const int MaximumEventBytes = 1024 * 1024;
    public const int MaximumRequestBytes = 1024 * 1024;
    public const int MaximumPages = 5;
    public const int CommentsPerPage = 100;
    public const int MaximumBodyCharacters = 65536;
    public const int MaximumLabelCount = 100;
    public const int MaximumLabelCharacters = 50;
    public const int MaximumErrorDetailCharacters = 200;
    public const int MaximumOwnerCharacters = 39;
    public const int MaximumRepositoryCharacters = 100;
    public const int MinimumCommitCharacters = 7;
    public const int MaximumCommitCharacters = 64;
    public const int MaximumEventNameCharacters = 128;
    public const int MaximumHeaderCharacters = 200;
    public const int ReadBufferBytes = 8192;

    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(10);

    public const string DefaultAccept = "application/vnd.github+json";
    public const string DiffAccept = "application/vnd.github.diff";
    public const string ApiVersion = "2022-11-28";
    public const string UserAgent = "CrystalCode.Integrations.GitHub";
    public const string DefaultApi = "https://api.github.com";
    public const string TruncationNote =
        "\nThe rest of the GitHub response was omitted because it exceeded 1 MiB.\n";
}
