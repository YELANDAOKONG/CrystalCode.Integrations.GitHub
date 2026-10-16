# Crystal Code GitHub

**A Crystal Code integration that reads and writes GitHub.**

This command talks to the GitHub REST API for a workflow the caller supplies. It does not decide what a review, issue, or label means. A caller that needs an endpoint this command does not name uses `request`.

[Command](#command) · [Library](#library) · [Build from source](#build-from-source)

## Command

GitHub Actions provides the environment. The token is read from `GITHUB_TOKEN` and is not printed.

| Variable | Required | Purpose |
| :--- | :--- | :--- |
| `GITHUB_TOKEN` | Yes | API credential |
| `GITHUB_REPOSITORY` | Yes | `owner/name` |
| `GITHUB_EVENT_NAME` | Yes | Event name for this job |
| `GITHUB_EVENT_PATH` | Yes | Path of the event payload file |
| `GITHUB_API_URL` | No | API root. Defaults to `https://api.github.com` |

```text
CrystalCode.Integrations.GitHub request <METHOD> <PATH> [--body-file <PATH>] [--accept <MEDIA>]
CrystalCode.Integrations.GitHub issue get <NUMBER>
CrystalCode.Integrations.GitHub issue comments <NUMBER>
CrystalCode.Integrations.GitHub issue comment <NUMBER> --body-file <PATH>
CrystalCode.Integrations.GitHub issue labels <NUMBER> --add <LABELS> | --set <LABELS>
CrystalCode.Integrations.GitHub pull get <NUMBER>
CrystalCode.Integrations.GitHub pull diff <NUMBER> [--output <PATH>]
CrystalCode.Integrations.GitHub pull review <NUMBER> --event <comment|approve|request-changes> --body-file <PATH> [--commit <SHA>]
CrystalCode.Integrations.GitHub event show
```

`request` sends one relative API path. The path cannot leave the API host, and the command does not follow redirects. `--body-file` for `request` is the raw JSON body. For `issue comment` and `pull review` it is the plain-text body. `--set` replaces labels; an empty value clears them. `--add` adds labels. Comment pages on the same host are followed. Each page is small enough that maximum-size comments fit in 1 MiB. The combined list stops at 500 comments or 5 MiB. If another page is still required, or the next link is on another host, the command fails instead of printing a partial list.

A response that is not JSON stops at 1 MiB and says the rest was omitted. A JSON response over 1 MiB fails. Exit 0 means the call succeeded. Exit 1 means the arguments, the environment, or GitHub rejected the call.

## Library

Reference this project and call `GitHubClient.SendAsync` for any other endpoint. `GitHubIssues` and `GitHubPullRequests` cover issues, comments, labels, pull requests, diffs, and reviews. `GitHubActions.Read` turns the job environment into a client. The HTTP client from `GitHubClient.CreateHttp` does not follow redirects.

## Build from source

You need the .NET 10 SDK.

```bash
dotnet build CrystalCode.Integrations.GitHub.sln
dotnet test CrystalCode.Integrations.GitHub.sln
```

The production project adds no NuGet packages. The test project uses xUnit and Microsoft.NET.Test.Sdk only.
