using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Actions;

/// <summary>
/// The GitHub Actions values this process needs. <see cref="ToString"/> omits
/// the token.
/// </summary>
public sealed record GitHubActionsEnvironment
{
    public GitHubActionsEnvironment(
        Uri api,
        string token,
        GitHubRepository repository,
        string eventName,
        string eventPath)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventPath);
        if (eventName.Length > GitHubLimits.MaximumEventNameCharacters
            || eventName.Contains('\r')
            || eventName.Contains('\n'))
        {
            throw new ArgumentException("GitHub event name is not valid.", nameof(eventName));
        }

        Api = api;
        Token = token;
        Repository = repository;
        EventName = eventName;
        EventPath = eventPath;
    }

    public Uri Api { get; }

    public string Token { get; }

    public GitHubRepository Repository { get; }

    public string EventName { get; }

    public string EventPath { get; }

    public GitHubClient CreateClient(HttpClient http) => new(http, Api, Token);

    public override string ToString() => $"{Repository} {EventName}";
}
