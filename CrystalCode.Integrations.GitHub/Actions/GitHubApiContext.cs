using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Actions;

/// <summary>
/// API credential and repository. <see cref="ToString"/> omits the token.
/// Event files are not part of this value.
/// </summary>
public sealed record GitHubApiContext
{
    public GitHubApiContext(Uri api, string token, GitHubRepository repository)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(repository);
        Api = api;
        Token = token;
        Repository = repository;
    }

    public Uri Api { get; }

    public string Token { get; }

    public GitHubRepository Repository { get; }

    public GitHubClient CreateClient(HttpClient http) => new(http, Api, Token);

    public override string ToString() => Repository.ToString();
}
