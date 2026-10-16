using System.Collections;

using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Actions;

/// <summary>Reads the GitHub Actions environment for one job.</summary>
public static class GitHubActions
{
    public const string TokenVariable = "GITHUB_TOKEN";
    public const string ApiUrlVariable = "GITHUB_API_URL";
    public const string RepositoryVariable = "GITHUB_REPOSITORY";
    public const string EventNameVariable = "GITHUB_EVENT_NAME";
    public const string EventPathVariable = "GITHUB_EVENT_PATH";

    public static GitHubActionsEnvironment ReadProcess()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value as string;
            }
        }

        return Read(environment);
    }

    public static GitHubActionsEnvironment Read(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var api = ReadApi(environment);
        return new GitHubActionsEnvironment(
            api.Api,
            api.Token,
            api.Repository,
            Required(environment, EventNameVariable),
            Required(environment, EventPathVariable));
    }

    /// <summary>
    /// Reads the API credential and repository. Event variables are not required.
    /// </summary>
    public static GitHubApiContext ReadApi(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var token = Required(environment, TokenVariable);
        if (token.Any(char.IsWhiteSpace))
        {
            throw new GitHubException($"{TokenVariable} is not valid.");
        }

        var apiText = Optional(environment, ApiUrlVariable) ?? GitHubLimits.DefaultApi;
        if (!Uri.TryCreate(apiText, UriKind.Absolute, out var api))
        {
            throw new GitHubException($"{ApiUrlVariable} is not a valid URL.");
        }

        return new GitHubApiContext(
            api,
            token,
            GitHubRepository.Parse(Required(environment, RepositoryVariable)));
    }

    private static string Required(IReadOnlyDictionary<string, string?> environment, string name)
    {
        if (!environment.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new GitHubException($"{name} is missing.");
        }

        return value;
    }

    private static string? Optional(IReadOnlyDictionary<string, string?> environment, string name)
    {
        if (!environment.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}
