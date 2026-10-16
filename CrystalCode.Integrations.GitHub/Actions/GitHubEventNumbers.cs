using System.Text.Json;

using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Actions;

/// <summary>
/// Issue and pull request numbers carried by a GitHub event. A pull request
/// number is also an issue number because GitHub uses it for both APIs.
/// </summary>
public sealed record GitHubEventNumbers(int? IssueNumber, int? PullRequestNumber)
{
    public static GitHubEventNumbers Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new GitHubException("GitHub event was not a JSON object.");
            }

            var root = document.RootElement;
            int? issue = null;
            int? pull = null;
            if (root.TryGetProperty("issue", out var issueElement) && issueElement.ValueKind == JsonValueKind.Object)
            {
                issue = Number(issueElement);
                if (issueElement.TryGetProperty("pull_request", out var marker)
                    && marker.ValueKind is JsonValueKind.Object)
                {
                    pull = issue;
                }
            }

            if (root.TryGetProperty("pull_request", out var pullElement) && pullElement.ValueKind == JsonValueKind.Object)
            {
                pull = Number(pullElement) ?? pull;
            }

            if (issue is null && pull is null && Number(root) is int rootNumber)
            {
                if (root.TryGetProperty("pull_request", out _))
                {
                    pull = rootNumber;
                }
                else
                {
                    issue = rootNumber;
                }
            }

            if (pull is not null && issue is null)
            {
                issue = pull;
            }

            return new GitHubEventNumbers(issue, pull);
        }
        catch (JsonException)
        {
            throw new GitHubException("GitHub event was not valid JSON.");
        }
    }

    private static int? Number(JsonElement element)
    {
        if (element.TryGetProperty("number", out var number) && number.TryGetInt32(out var value) && value > 0)
        {
            return value;
        }

        return null;
    }
}
