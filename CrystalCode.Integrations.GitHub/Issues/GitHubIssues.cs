using System.Text;
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
        GitHubResponse? first = null;
        var pages = new List<string>();
        var bytes = 0;
        for (var pageNumber = 0; pageNumber < GitHubLimits.MaximumCommentPages; pageNumber++)
        {
            var page = await client.SendAsync(new GitHubRequest(HttpMethod.Get, path), cancellationToken).ConfigureAwait(false);
            first ??= page;
            pages.Add(page.Body);
            bytes += Encoding.UTF8.GetByteCount(page.Body);
            if (bytes > GitHubLimits.MaximumCommentListBytes)
            {
                throw new GitHubException("GitHub comment list exceeded 5 MiB.");
            }

            if (!GitHubLinks.TryFollow(client.Api, page.Headers, out var next, out var rejected))
            {
                if (rejected)
                {
                    throw new GitHubException("GitHub comment list is incomplete.");
                }

                return pages.Count == 1
                    ? first
                    : new GitHubResponse(first.StatusCode, MergeArrays(pages), false, first.Headers);
            }

            path = next ?? throw new GitHubException("GitHub comment list is incomplete.");
        }

        throw new GitHubException($"GitHub comment list exceeded {GitHubLimits.MaximumComments} comments.");
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
