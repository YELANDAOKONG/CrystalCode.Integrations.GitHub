# Crystal Code GitHub

[![Tests](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/test.yml)
[![Release](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/release.yml/badge.svg)](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/release.yml)

**A Crystal Code integration that reads and writes GitHub.**

This command talks to the GitHub REST API for a workflow the caller supplies. It does not decide what a review, issue, or label means. A caller that needs an endpoint this command does not name uses `request`.

[Command](#command) · [Library](#library) · [Build from source](#build-from-source)

## Command

GitHub Actions provides the environment. The token is read from `GITHUB_TOKEN` and is not printed.

| Variable | Required | Purpose |
| :--- | :--- | :--- |
| `GITHUB_TOKEN` | Yes | API credential. It is read from the environment and is not an argument. |
| `GITHUB_REPOSITORY` | Yes | `owner/name` |
| `GITHUB_EVENT_NAME` | For `event show` | Event name for this job |
| `GITHUB_EVENT_PATH` | For `event show` | Path of the event payload file |
| `GITHUB_API_URL` | No | API root. Defaults to `https://api.github.com` |

```text
CrystalCode.Integrations.GitHub.Command request (<METHOD> <PATH> | --method <METHOD> --path <PATH>) [--body-file <PATH> | --body <JSON>] [--accept <MEDIA>]
CrystalCode.Integrations.GitHub.Command issue get (<NUMBER> | --number <NUMBER>)
CrystalCode.Integrations.GitHub.Command issue comments (<NUMBER> | --number <NUMBER>)
CrystalCode.Integrations.GitHub.Command issue comment (<NUMBER> | --number <NUMBER>) (--body-file <PATH> | --body <TEXT>)
CrystalCode.Integrations.GitHub.Command issue labels (<NUMBER> | --number <NUMBER>) --add <LABELS> | --set <LABELS>
CrystalCode.Integrations.GitHub.Command pull get (<NUMBER> | --number <NUMBER>)
CrystalCode.Integrations.GitHub.Command pull diff (<NUMBER> | --number <NUMBER>) [--output <PATH>]
CrystalCode.Integrations.GitHub.Command pull review (<NUMBER> | --number <NUMBER>) --event <comment|approve|request-changes> (--body-file <PATH> | --body <TEXT>) [--commit <SHA>]
CrystalCode.Integrations.GitHub.Command event show
```

`request` sends one relative API path. The path cannot leave the API host, and the command does not follow redirects. `--body` and `--body-file` are mutually exclusive. For `request`, either one is the raw JSON body. For `issue comment` and `pull review`, either one is the plain-text body. `--number` replaces the positional number; do not pass both. `--set` replaces labels; an empty value clears them. `--add` adds labels. Comment pages on the same host are followed. Each page is small enough that maximum-size comments fit in 1 MiB. The combined list stops at 500 comments or 5 MiB. If another page is still required, or the next link is on another host, the command fails instead of printing a partial list.

A response that is not JSON stops at 1 MiB and says the rest was omitted. A JSON response over 1 MiB fails. Exit 0 means the call succeeded. Exit 1 means the arguments, the environment, or GitHub rejected the call.

## Crystal Code tool

`CrystalCode.Integrations.GitHub.Tools` is a native Crystal Code tool assembly (`runner: dotnet`). The agent calls `github_issue_get`, `github_pull_review`, and the other `ITool` names. It does not call `bash`, and the tools do not start this command.

Publish the framework-dependent class library and install that directory as `~/.crystal/tools/GitHub/` (or the project equivalent under `.crystal/tools/`). `tools.json` names `CrystalCode.Integrations.GitHub.Tools.dll`. Do not copy `Crystal.dll` or `Crystal.Tools.dll` into that directory; the host already loaded those contracts. Building this project needs the Crystal source. It defaults to `../../Crystal`. Pass `-p:CrystalRoot` when Crystal is somewhere else.

Put `GITHUB_TOKEN` and `GITHUB_REPOSITORY` in the Crystal Code process environment. The host does not accept the token as a tool argument. `GITHUB_EVENT_NAME` and `GITHUB_EVENT_PATH` are required only for `github_event_show`. Writes (`github_request`, `github_issue_comment`, `github_issue_labels`, `github_pull_review`) are work-only. `github_pull_diff` treats `output` as a workspace path.

## Library

Reference this project and call `GitHubClient.SendAsync` for any other endpoint. `GitHubIssues` and `GitHubPullRequests` cover issues, comments, labels, pull requests, diffs, and reviews. `GitHubActions.ReadApi` reads the credential and repository. `GitHubActions.Read` also reads the job event. The HTTP client from `GitHubClient.CreateHttp` does not follow redirects.

## Build from source

You need the .NET 10 SDK.

```bash
dotnet build CrystalCode.Integrations.GitHub.sln
dotnet test CrystalCode.Integrations.GitHub.sln
```

The production project adds no NuGet packages. The test project uses xUnit and Microsoft.NET.Test.Sdk only.
