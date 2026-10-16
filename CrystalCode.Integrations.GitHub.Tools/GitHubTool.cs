using System.Collections;
using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Integrations.GitHub.Actions;

namespace CrystalCode.Integrations.GitHub.Tools;

/// <summary>
/// Shared call path for the native tools. The host does not pass
/// <c>GITHUB_TOKEN</c>, so each call reads the process environment unless a
/// test substitutes a dictionary.
/// </summary>
internal static class GitHubTool
{
    internal static Func<IReadOnlyDictionary<string, string?>> ReadEnvironment { get; set; } =
        ReadProcessEnvironment;

    internal static void UseProcessEnvironment() => ReadEnvironment = ReadProcessEnvironment;

    internal static ToolDefinition Define(string name, string description, string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return new ToolDefinition(name, document.RootElement, description);
    }

    internal static async ValueTask<ToolOutput> InvokeApiAsync(
        ToolCall call,
        Func<GitHubClient, GitHubRepository, JsonElement, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var arguments = GitHubArguments.Object(call);
            var session = GitHubActions.ReadApi(ReadEnvironment());
            using var http = GitHubClient.CreateHttp();
            var client = session.CreateClient(http);
            var text = await operation(client, session.Repository, arguments, cancellationToken)
                .ConfigureAwait(false);
            return new ToolOutput(text);
        }
        catch (Exception exception) when (IsCallerError(exception))
        {
            return new ToolOutput(exception.Message, ToolResultStatus.Failure);
        }
    }

    internal static async ValueTask<ToolOutput> InvokeEventAsync(
        ToolCall call,
        Func<GitHubActionsEnvironment, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            GitHubArguments.Object(call);
            var text = await operation(GitHubActions.Read(ReadEnvironment()), cancellationToken)
                .ConfigureAwait(false);
            return new ToolOutput(text);
        }
        catch (Exception exception) when (IsCallerError(exception))
        {
            return new ToolOutput(exception.Message, ToolResultStatus.Failure);
        }
    }

    internal static string Text(GitHubResponse response) => response.Body;

    private static IReadOnlyDictionary<string, string?> ReadProcessEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value as string;
            }
        }

        return environment;
    }

    private static bool IsCallerError(Exception exception) =>
        exception is GitHubException or ArgumentException or JsonException or IOException or UnauthorizedAccessException;
}
