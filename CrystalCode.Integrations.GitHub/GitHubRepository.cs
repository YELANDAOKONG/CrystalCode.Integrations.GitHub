namespace CrystalCode.Integrations.GitHub;

/// <summary>An owner and repository the API path is built from.</summary>
public sealed record GitHubRepository
{
    public GitHubRepository(string owner, string name)
    {
        if (!IsName(owner, GitHubLimits.MaximumOwnerCharacters))
        {
            throw new ArgumentException(
                "GitHub owner must be 1–39 letters, digits, hyphens, underscores, or periods.",
                nameof(owner));
        }

        if (!IsName(name, GitHubLimits.MaximumRepositoryCharacters))
        {
            throw new ArgumentException(
                "GitHub repository name must be 1–100 letters, digits, hyphens, underscores, or periods.",
                nameof(name));
        }

        Owner = owner;
        Name = name;
    }

    public string Owner { get; }

    public string Name { get; }

    public string ApiPath => $"/repos/{Owner}/{Name}";

    public static GitHubRepository Parse(string ownerAndName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerAndName);
        var slash = ownerAndName.IndexOf('/');
        if (slash <= 0 || slash != ownerAndName.LastIndexOf('/'))
        {
            throw new ArgumentException("GitHub repository must be owner/name.", nameof(ownerAndName));
        }

        return new GitHubRepository(ownerAndName[..slash], ownerAndName[(slash + 1)..]);
    }

    public override string ToString() => $"{Owner}/{Name}";

    private static bool IsName(string value, int maximum)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximum || value is "." or "..")
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
