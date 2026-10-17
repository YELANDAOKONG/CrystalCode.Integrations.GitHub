using System.Text.Json;

using CrystalCode.Integrations.GitHub.Actions;
using CrystalCode.Integrations.GitHub.Repositories;

namespace CrystalCode.Integrations.GitHub.Tools;

internal static class GitHubCodeSnapshot
{
    public static async Task<string> ResolveCommitAsync(
        GitHubClient client,
        GitHubRepository repository,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        _ = Required(environment, GitHubActions.EventNameVariable);
        var eventPath = Required(environment, GitHubActions.EventPathVariable);
        var eventJson = await GitHubEventFile.ReadAsync(eventPath, cancellationToken).ConfigureAwait(false);
        using var eventDocument = JsonDocument.Parse(eventJson);
        var root = eventDocument.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new GitHubException("GitHub event was not a JSON object.");
        }

        if (TryPullRequestHead(root, out var headSha))
        {
            GitHubCodeRepository.ValidateCommit(headSha);
            return headSha;
        }

        if (IsPullRequestComment(root))
        {
            var numbers = GitHubEventNumbers.Read(eventJson);
            if (numbers.PullRequestNumber is not int pullRequestNumber)
            {
                throw new GitHubException("GitHub event did not contain a pull request number.");
            }

            var response = await client.SendAsync(
                new GitHubRequest(HttpMethod.Get, $"{repository.ApiPath}/pulls/{pullRequestNumber}"),
                cancellationToken).ConfigureAwait(false);
            using var pull = JsonDocument.Parse(response.Body);
            if (!pull.RootElement.TryGetProperty("head", out var head)
                || !head.TryGetProperty("sha", out var sha)
                || sha.ValueKind != JsonValueKind.String)
            {
                throw new GitHubException("GitHub pull request response did not contain a head commit.");
            }

            var commit = sha.GetString()!;
            GitHubCodeRepository.ValidateCommit(commit);
            return commit;
        }

        var reference = Optional(environment, "GITHUB_CODE_REF");
        if (reference is null)
        {
            var repositoryResponse = await client.SendAsync(
                new GitHubRequest(HttpMethod.Get, repository.ApiPath),
                cancellationToken).ConfigureAwait(false);
            using var repositoryDocument = JsonDocument.Parse(repositoryResponse.Body);
            if (!repositoryDocument.RootElement.TryGetProperty("default_branch", out var branch)
                || branch.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(branch.GetString()))
            {
                throw new GitHubException("GitHub repository response did not contain a default branch.");
            }

            reference = branch.GetString();
        }

        return await GitHubCodeRepository.ResolveCommitAsync(client, repository, reference!, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryPullRequestHead(JsonElement root, out string commit)
    {
        if (root.TryGetProperty("pull_request", out var pull)
            && pull.ValueKind == JsonValueKind.Object
            && pull.TryGetProperty("head", out var head)
            && head.TryGetProperty("sha", out var sha)
            && sha.ValueKind == JsonValueKind.String)
        {
            commit = sha.GetString()!;
            return true;
        }

        commit = string.Empty;
        return false;
    }

    private static bool IsPullRequestComment(JsonElement root) =>
        root.TryGetProperty("issue", out var issue)
        && issue.ValueKind == JsonValueKind.Object
        && issue.TryGetProperty("pull_request", out var pull)
        && pull.ValueKind == JsonValueKind.Object;

    private static string Required(IReadOnlyDictionary<string, string?> environment, string key)
    {
        if (!environment.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new GitHubException($"{key} is missing.");
        }

        return value;
    }

    private static string? Optional(IReadOnlyDictionary<string, string?> environment, string key) =>
        environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
