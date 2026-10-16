using System.Text.Json;

using Crystal.Tools;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>Sends one relative GitHub REST request.</summary>
public sealed class GitHubRequestTool : ITool
{
    private static readonly ToolDefinition Cached = GitHubTool.Define(
        "github_request",
        "Send one relative GitHub REST request. The token and repository come from the process environment.",
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "method": { "type": "string", "enum": ["GET", "POST", "PUT", "PATCH", "DELETE"] },
            "path": { "type": "string" },
            "body": { "type": "string" },
            "accept": { "type": "string" }
          },
          "required": ["method", "path"]
        }
        """);

    public ToolDefinition Definition => Cached;

    public ValueTask<ToolOutput> InvokeAsync(ToolCall call, CancellationToken cancellationToken = default) =>
        GitHubTool.InvokeApiAsync(call, RunAsync, cancellationToken);

    private static async Task<string> RunAsync(
        GitHubClient client,
        GitHubRepository repository,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        _ = repository;
        var response = await client.SendAsync(
            new GitHubRequest(
                Method(GitHubArguments.Required(arguments, "method")),
                GitHubArguments.Required(arguments, "path"),
                GitHubArguments.Optional(arguments, "body"),
                GitHubArguments.Optional(arguments, "accept")),
            cancellationToken).ConfigureAwait(false);
        return GitHubTool.Text(response);
    }

    private static HttpMethod Method(string name) => name.ToUpperInvariant() switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "PATCH" => HttpMethod.Patch,
        "DELETE" => HttpMethod.Delete,
        _ => throw new GitHubException("HTTP method must be GET, POST, PUT, PATCH, or DELETE.")
    };
}
