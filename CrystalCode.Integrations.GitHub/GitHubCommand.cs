using System.Globalization;

using CrystalCode.Integrations.GitHub.Actions;
using CrystalCode.Integrations.GitHub.Issues;
using CrystalCode.Integrations.GitHub.PullRequests;

namespace CrystalCode.Integrations.GitHub;

/// <summary>
/// Command line for GitHub reads and writes. Endpoints without a subcommand
/// go through <c>request</c>.
/// </summary>
public static class GitHubCommand
{
    public const string Usage =
        """
        CrystalCode.Integrations.GitHub request <METHOD> <PATH> [--body-file <PATH>] [--accept <MEDIA>]
        CrystalCode.Integrations.GitHub issue get <NUMBER>
        CrystalCode.Integrations.GitHub issue comments <NUMBER>
        CrystalCode.Integrations.GitHub issue comment <NUMBER> --body-file <PATH>
        CrystalCode.Integrations.GitHub issue labels <NUMBER> --add <LABELS> | --set <LABELS>
        CrystalCode.Integrations.GitHub pull get <NUMBER>
        CrystalCode.Integrations.GitHub pull diff <NUMBER> [--output <PATH>]
        CrystalCode.Integrations.GitHub pull review <NUMBER> --event <comment|approve|request-changes> --body-file <PATH> [--commit <SHA>]
        CrystalCode.Integrations.GitHub event show
        """;

    public static async Task<int> RunAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> environment,
        Func<HttpClient> httpFactory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(httpFactory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 0 || args[0] is not ("request" or "issue" or "pull" or "event"))
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 1;
        }

        try
        {
            var context = GitHubActions.Read(environment);
            using var http = httpFactory();
            var client = context.CreateClient(http);
            switch (args[0])
            {
                case "request":
                    await RequestAsync(client, args, output, cancellationToken).ConfigureAwait(false);
                    break;
                case "issue":
                    await IssueAsync(client, context.Repository, args, output, cancellationToken).ConfigureAwait(false);
                    break;
                case "pull":
                    await PullAsync(client, context.Repository, args, output, cancellationToken).ConfigureAwait(false);
                    break;
                case "event":
                    await EventAsync(context, args, output, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await error.WriteLineAsync(Usage).ConfigureAwait(false);
                    return 1;
            }

            return 0;
        }
        catch (Exception exception) when (exception is GitHubException or ArgumentException)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task RequestAsync(
        GitHubClient client,
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var parsed = Parse(args, 1);
        RequireOnly(parsed.Options, "--body-file", "--accept");
        if (parsed.Positionals.Count != 2)
        {
            throw new GitHubException(Usage);
        }

        string? body = null;
        if (Option(parsed.Options, "--body-file") is string bodyFile)
        {
            body = await ReadFileAsync(bodyFile, GitHubLimits.MaximumRequestBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        var response = await client.SendAsync(
            new GitHubRequest(Method(parsed.Positionals[0]), parsed.Positionals[1], body, Option(parsed.Options, "--accept")),
            cancellationToken).ConfigureAwait(false);
        await WriteBodyAsync(output, response.Body).ConfigureAwait(false);
    }

    private static async Task IssueAsync(
        GitHubClient client,
        GitHubRepository repository,
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new GitHubException(Usage);
        }

        var parsed = Parse(args, 2);
        switch (args[1])
        {
            case "get":
                RequireOnly(parsed.Options);
                await WriteAsync(
                    output,
                    await GitHubIssues.GetAsync(client, repository, OneNumber(parsed.Positionals), cancellationToken)
                        .ConfigureAwait(false)).ConfigureAwait(false);
                return;
            case "comments":
                RequireOnly(parsed.Options);
                await WriteAsync(
                    output,
                    await GitHubIssues.ListCommentsAsync(client, repository, OneNumber(parsed.Positionals), cancellationToken)
                        .ConfigureAwait(false)).ConfigureAwait(false);
                return;
            case "comment":
                RequireOnly(parsed.Options, "--body-file");
                var comment = await ReadFileAsync(
                    RequiredOption(parsed.Options, "--body-file"),
                    GitHubLimits.MaximumRequestBytes,
                    cancellationToken).ConfigureAwait(false);
                await WriteAsync(
                    output,
                    await GitHubIssues.CreateCommentAsync(
                        client,
                        repository,
                        OneNumber(parsed.Positionals),
                        comment,
                        cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            case "labels":
                RequireOnly(parsed.Options, "--add", "--set");
                await LabelsAsync(client, repository, parsed, output, cancellationToken).ConfigureAwait(false);
                return;
            default:
                throw new GitHubException(Usage);
        }
    }

    private static async Task LabelsAsync(
        GitHubClient client,
        GitHubRepository repository,
        ParsedArgs parsed,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var add = Option(parsed.Options, "--add");
        var set = Option(parsed.Options, "--set");
        if ((add is null && set is null) || (add is not null && set is not null))
        {
            throw new GitHubException("Use either --add or --set.");
        }

        var number = OneNumber(parsed.Positionals);
        var response = add is not null
            ? await GitHubIssues.AddLabelsAsync(client, repository, number, Labels(add, allowEmpty: false), cancellationToken)
                .ConfigureAwait(false)
            : await GitHubIssues.SetLabelsAsync(client, repository, number, Labels(set!, allowEmpty: true), cancellationToken)
                .ConfigureAwait(false);
        await WriteAsync(output, response).ConfigureAwait(false);
    }

    private static async Task PullAsync(
        GitHubClient client,
        GitHubRepository repository,
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new GitHubException(Usage);
        }

        var parsed = Parse(args, 2);
        switch (args[1])
        {
            case "get":
                RequireOnly(parsed.Options);
                await WriteAsync(
                    output,
                    await GitHubPullRequests.GetAsync(client, repository, OneNumber(parsed.Positionals), cancellationToken)
                        .ConfigureAwait(false)).ConfigureAwait(false);
                return;
            case "diff":
                RequireOnly(parsed.Options, "--output");
                var diff = await GitHubPullRequests.GetDiffAsync(
                    client,
                    repository,
                    OneNumber(parsed.Positionals),
                    cancellationToken).ConfigureAwait(false);
                if (Option(parsed.Options, "--output") is string outputPath)
                {
                    await WriteFileAsync(outputPath, diff.Body, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteBodyAsync(output, diff.Body).ConfigureAwait(false);
                return;
            case "review":
                RequireOnly(parsed.Options, "--event", "--body-file", "--commit");
                var review = await ReadFileAsync(
                    RequiredOption(parsed.Options, "--body-file"),
                    GitHubLimits.MaximumRequestBytes,
                    cancellationToken).ConfigureAwait(false);
                await WriteAsync(
                    output,
                    await GitHubPullRequests.CreateReviewAsync(
                        client,
                        repository,
                        OneNumber(parsed.Positionals),
                        review,
                        ReviewEvent(RequiredOption(parsed.Options, "--event")),
                        Option(parsed.Options, "--commit"),
                        cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            default:
                throw new GitHubException(Usage);
        }
    }

    private static async Task EventAsync(
        GitHubActionsEnvironment context,
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args.Length != 2 || args[1] != "show")
        {
            throw new GitHubException(Usage);
        }

        await WriteBodyAsync(output, await GitHubEventFile.ReadAsync(context.EventPath, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);
    }

    private static async Task WriteAsync(TextWriter output, GitHubResponse response) =>
        await WriteBodyAsync(output, response.Body).ConfigureAwait(false);

    private static async Task WriteBodyAsync(TextWriter output, string body)
    {
        await output.WriteAsync(body).ConfigureAwait(false);
        if (body.Length == 0 || body[^1] != '\n')
        {
            await output.WriteAsync("\n").ConfigureAwait(false);
        }
    }

    private static async Task WriteFileAsync(string path, string body, CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllTextAsync(path, body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GitHubException("Could not write the GitHub response.");
        }
    }

    private static async Task<string> ReadFileAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new GitHubException($"File was not found: {path}");
        }

        if (info.Length > maximumBytes)
        {
            throw new GitHubException($"File exceeded the size limit: {path}");
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GitHubException($"File could not be read: {path}");
        }
    }

    private static int OneNumber(IReadOnlyList<string> positionals)
    {
        if (positionals.Count != 1
            || !int.TryParse(positionals[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new GitHubException("Issue and pull request numbers start at 1.");
        }

        return number;
    }

    private static IReadOnlyList<string> Labels(string text, bool allowEmpty)
    {
        if (text.Length == 0)
        {
            if (allowEmpty)
            {
                return [];
            }

            throw new GitHubException("At least one label is required.");
        }

        var labels = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0)
        {
            if (allowEmpty)
            {
                return [];
            }

            throw new GitHubException("At least one label is required.");
        }

        return labels;
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

    private static PullRequestReviewEvent ReviewEvent(string value) => value switch
    {
        "comment" => PullRequestReviewEvent.Comment,
        "approve" => PullRequestReviewEvent.Approve,
        "request-changes" => PullRequestReviewEvent.RequestChanges,
        _ => throw new GitHubException("Review event must be comment, approve, or request-changes.")
    };

    private static ParsedArgs Parse(string[] args, int start)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = start; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(arg);
                continue;
            }

            if (arg.Length == 2
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new GitHubException($"Missing value for {arg}.");
            }

            if (!options.TryAdd(arg, args[index + 1]))
            {
                throw new GitHubException($"Duplicate option {arg}.");
            }

            index++;
        }

        return new ParsedArgs(positionals, options);
    }

    private static void RequireOnly(IReadOnlyDictionary<string, string> options, params string[] allowed)
    {
        foreach (var key in options.Keys)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                throw new GitHubException($"Unknown option {key}.");
            }
        }
    }

    private static string RequiredOption(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || value.Length == 0)
        {
            throw new GitHubException($"Missing {name}.");
        }

        return value;
    }

    private static string? Option(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) ? value : null;

    private sealed record ParsedArgs(
        IReadOnlyList<string> Positionals,
        IReadOnlyDictionary<string, string> Options);
}
