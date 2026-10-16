using System.Text.Json;
using System.Text.Json.Nodes;

using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Issues;

/// <summary>Issue reads and writes. Other issue endpoints use <see cref="GitHubClient.SendAsync"/>.</summary>
public static class GitHubIssues
{
    public static Task<GitHubResponse> GetAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.SendAsync(new GitHubRequest(HttpMethod.Get, GitHubPaths.Issue(repository, number)), cancellationToken);
    }

    public static async Task<GitHubResponse> ListCommentsAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var path = GitHubPaths.Issue(repository, number) + "/comments?per_page=" + GitHubLimits.CommentsPerPage;
        var first = await client.SendAsync(new GitHubRequest(HttpMethod.Get, path), cancellationToken).ConfigureAwait(false);
        var pages = new List<string> { first.Body };
        var truncated = false;
        var followed = 1;
        var more = Follow(client, first, ref truncated);
        while (more is not null && followed < GitHubLimits.MaximumPages)
        {
            var page = await client.SendAsync(new GitHubRequest(HttpMethod.Get, more), cancellationToken).ConfigureAwait(false);
            pages.Add(page.Body);
            followed++;
            more = Follow(client, page, ref truncated);
        }

        if (more is not null)
        {
            truncated = true;
        }

        if (pages.Count == 1)
        {
            return truncated ? first with { Truncated = true } : first;
        }

        return new GitHubResponse(first.StatusCode, MergeArrays(pages), truncated, first.Headers);
    }

    public static Task<GitHubResponse> CreateCommentAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        string body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        RequireBody(body);
        var request = new GitHubRequest(
            HttpMethod.Post,
            GitHubPaths.Issue(repository, number) + "/comments",
            new JsonObject { ["body"] = body }.ToJsonString());
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<GitHubResponse> AddLabelsAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (labels is null || labels.Count == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        return SendLabelsAsync(client, repository, number, labels, HttpMethod.Post, cancellationToken);
    }

    public static Task<GitHubResponse> SetLabelsAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        IReadOnlyList<string> labels,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(labels);
        return SendLabelsAsync(client, repository, number, labels, HttpMethod.Put, cancellationToken);
    }

    private static Task<GitHubResponse> SendLabelsAsync(
        GitHubClient client,
        GitHubRepository repository,
        int number,
        IReadOnlyList<string> labels,
        HttpMethod method,
        CancellationToken cancellationToken)
    {
        var request = new GitHubRequest(
            method,
            GitHubPaths.Issue(repository, number) + "/labels",
            LabelsJson(labels));
        return client.SendAsync(request, cancellationToken);
    }

    private static string? Follow(GitHubClient client, GitHubResponse response, ref bool truncated)
    {
        if (!GitHubLinks.TryFollow(client.Api, response.Headers, out var path, out var rejected))
        {
            if (rejected)
            {
                truncated = true;
            }

            return null;
        }

        return path;
    }

    private static string MergeArrays(IReadOnlyList<string> pages)
    {
        var merged = new JsonArray();
        foreach (var page in pages)
        {
            JsonNode? parsed;
            try
            {
                parsed = JsonNode.Parse(page);
            }
            catch (JsonException)
            {
                throw new GitHubException("GitHub response was not a JSON array.");
            }

            if (parsed is not JsonArray array)
            {
                throw new GitHubException("GitHub response was not a JSON array.");
            }

            foreach (var item in array)
            {
                merged.Add(item?.DeepClone());
            }
        }

        return merged.ToJsonString();
    }

    private static string LabelsJson(IReadOnlyList<string> labels)
    {
        if (labels.Count > GitHubLimits.MaximumLabelCount)
        {
            throw new ArgumentException("At most 100 labels can be sent at once.", nameof(labels));
        }

        var array = new JsonArray();
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label)
                || label.Length > GitHubLimits.MaximumLabelCharacters
                || label.Contains('\r')
                || label.Contains('\n'))
            {
                throw new ArgumentException("A GitHub label must be 1–50 characters on one line.", nameof(labels));
            }

            array.Add(label);
        }

        return new JsonObject { ["labels"] = array }.ToJsonString();
    }

    private static void RequireBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("GitHub comment body is required.", nameof(body));
        }

        if (body.Length > GitHubLimits.MaximumBodyCharacters)
        {
            throw new ArgumentException("GitHub comment body exceeded 65536 characters.", nameof(body));
        }
    }
}
