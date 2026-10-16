using System.Text.Json.Nodes;

using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.PullRequests;

/// <summary>
/// Pull request reads and reviews. Other pull request endpoints use
/// <see cref="GitHubClient.SendAsync"/>.
/// </summary>
public static class GitHubPullRequests
{
    public static Task<GitHubResponse> GetAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendAsync(new GitHubRequest(HttpMethod.Get, GitHubPaths.Pull(repository, number)), cancellationToken);
    }

    public static Task<GitHubResponse> GetDiffAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var request = new GitHubRequest(
            HttpMethod.Get,
            GitHubPaths.Pull(repository, number),
            accept: GitHubLimits.DiffAccept);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<GitHubResponse> CreateReviewAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        string body,
        PullRequestReviewEvent reviewEvent,
        string? commitId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("GitHub review body is required.", nameof(body));
        }

        if (body.Length > GitHubLimits.MaximumBodyCharacters)
        {
            throw new ArgumentException("GitHub review body exceeded 65536 characters.", nameof(body));
        }

        if (commitId is not null && !IsCommit(commitId))
        {
            throw new ArgumentException("GitHub commit id must be 7–64 hexadecimal characters.", nameof(commitId));
        }

        var payload = new JsonObject
        {
            ["body"] = body,
            ["event"] = EventName(reviewEvent)
        };
        if (commitId is not null)
        {
            payload["commit_id"] = commitId;
        }

        var request = new GitHubRequest(
            HttpMethod.Post,
            GitHubPaths.Pull(repository, number) + "/reviews",
            payload.ToJsonString());
        return client.SendAsync(request, cancellationToken);
    }

    private static string EventName(PullRequestReviewEvent reviewEvent) => reviewEvent switch
    {
        PullRequestReviewEvent.Comment => "COMMENT",
        PullRequestReviewEvent.Approve => "APPROVE",
        PullRequestReviewEvent.RequestChanges => "REQUEST_CHANGES",
        _ => throw new ArgumentOutOfRangeException(nameof(reviewEvent))
    };

    private static bool IsCommit(string commitId)
    {
        if (commitId.Length < GitHubLimits.MinimumCommitCharacters
            || commitId.Length > GitHubLimits.MaximumCommitCharacters)
        {
            return false;
        }

        foreach (var character in commitId)
        {
            if (character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
