# Crystal Code GitHub

[![Tests](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/test.yml)
[![Release](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/release.yml/badge.svg)](https://github.com/YELANDAOKONG/CrystalCode.Integrations.GitHub/actions/workflows/release.yml)

**Run Crystal Code in GitHub workflows for pull request checks, issue checks, issue label classification, and other repository tasks.**

This repository is intended to provide the complete GitHub integration, not just a REST client or command-line tool. Its scope includes a GitHub Action entry point (`action.yml`), a program that converts environment variables into Crystal Code configuration, CI bootstrap tooling to obtain and assemble Crystal Code with this repository's GitHub tools and prompt attachments, and default event-triggered workflow YAML templates.

The repository includes a Linux x64 composite Action, a configuration/bootstrap program, task prompt attachments, consumer workflow templates, and the underlying GitHub API library and tools. The release workflow builds an Action package containing the bootstrap executable, tools, and prompts. The download-based Action requires a release containing `CrystalCode.Integrations.GitHub.Action-linux-x64.zip`; older command-only releases do not work with it. See [AGENTS.md](AGENTS.md) for project intent and contributor guidance.

The existing command talks to the GitHub REST API for a workflow the caller supplies. It does not itself decide what a review, issue, or label means; that behavior belongs to the Crystal Code task and prompts. A caller that needs an endpoint this command does not name uses `request`.

[GitHub Action](#github-action) · [First deployment](#first-deployment) · [Provider and model](#provider-and-model) · [Configuration](#configuration) · [Security](#security) · [Command](#command) · [Library](#library) · [Build from source](#build-from-source)

## GitHub Action

This integration is a download-based Linux x64 composite Action. The release used by the Action must contain both of these assets:

```text
CrystalCode-linux-x64.zip
CrystalCode.Integrations.GitHub.Action-linux-x64.zip
```

The first asset comes from [CrystalCode](https://github.com/YELANDAOKONG/CrystalCode). The second comes from this repository. A command-only integration ZIP is not an Action package.

The Action runs on `ubuntu-latest` (Linux x64). Pin the Action ref, `crystal-version`, and `integration-version` to compatible published releases in production. Both version inputs default to `latest`. A self-hosted runner also needs `bash`, `curl`, `unzip`, and the native dependencies of the self-contained .NET binaries.

The Action does not checkout the target repository. It creates a temporary Crystal Code home and an empty workspace, reads GitHub data through the API, installs only the packaged GitHub tools and prompts, and removes the `CRYSTAL_GITHUB_*` input variables before starting Crystal Code.

### First deployment

1. Create a repository Secret at **Settings → Secrets and variables → Actions**.

   For the default DeepSeek provider, create:

   ```text
   CRYSTAL_API_KEY=<your DeepSeek API key>
   ```

2. Create `.github/workflows/crystal-issue-check.yml` in the target repository.

3. Copy the [Issue check template](workflow-templates/issue-check.yml), or use this pinned version:

   ```yaml
   name: Crystal Code issue check

   on:
     issues:
       types: [opened, edited]

   permissions:
     contents: read
     issues: read

   jobs:
     check:
       runs-on: ubuntu-latest
       timeout-minutes: 15
       steps:
         - uses: YELANDAOKONG/CrystalCode.Integrations.GitHub@v0.0.1
           env:
             GITHUB_TOKEN: ${{ github.token }}
             CRYSTAL_API_KEY: ${{ secrets.CRYSTAL_API_KEY }}
           with:
             task: issue-check
             crystal-version: v0.0.1
             integration-version: v0.0.1
   ```

   Replace `v0.0.1` with a compatible published tag. The Action ref and `integration-version` should refer to this integration release; `crystal-version` should refer to the Crystal Code release.

4. Open or edit an issue and inspect the workflow run under the repository's **Actions** tab. Checks print their assessment in the run log; they do not post an issue comment or PR review.

The repository also includes ready-to-copy templates for [PR checks](workflow-templates/pull-request-check.yml), [Issue checks](workflow-templates/issue-check.yml), and [Issue label classification](workflow-templates/issue-label.yml).

```yaml
- uses: YELANDAOKONG/CrystalCode.Integrations.GitHub@v0.0.1
  env:
    GITHUB_TOKEN: ${{ github.token }}
    CRYSTAL_API_KEY: ${{ secrets.CRYSTAL_API_KEY }}
  with:
    task: issue-check
    crystal-version: v0.0.1
    integration-version: v0.0.1
```

| Task | Behavior | Suggested token permissions |
| :--- | :--- | :--- |
| `pull-request-check` (default) | Read the PR metadata and diff; print findings in the run log | `contents: read`, `pull-requests: read` |
| `issue-check` | Read the issue and comments; print an assessment | `contents: read`, `issues: read` |
| `issue-label` | Discover existing labels and add applicable missing labels | `contents: read`, `issues: write` |
| `custom` | Run the required maintainer-supplied `prompt` in Work mode | Grant only the permissions needed by the task |

Checks use Plan mode; label and custom tasks use Work mode. The default approval mode is `review`, which uses the session model unless `config.approvalModel` selects another. These approval rules and prompts are not an operating-system sandbox. A successful process exit means the agent turn completed, not that the reviewed code passed a deterministic quality gate. Reports are currently written to the run log, not posted as PR reviews or issue comments.

The PR template skips draft PRs and PRs from forks because fork workflows do not receive model API secrets. It does not use `pull_request_target`. Do not add a checkout step: the integration intentionally does not execute contributor code.

### Provider and model

The default is `deepseek` with model `deepseek-flash`. For another provider, set `provider`, `model`, and `providers` together. The provider name is an identifier; the model id must be listed under that provider.

For an OpenAI-compatible endpoint:

```yaml
steps:
  - uses: YELANDAOKONG/CrystalCode.Integrations.GitHub@v0.0.1
    env:
      GITHUB_TOKEN: ${{ github.token }}
      PRIVATE_LLM_API_KEY: ${{ secrets.PRIVATE_LLM_API_KEY }}
    with:
      task: issue-check
      provider: private-gateway
      model: internal-reasoning-model
      providers: |
        {
          "private-gateway": {
            "protocol": "openai",
            "baseUri": "https://llm.example.com/v1/",
            "apiKeyEnvironment": "PRIVATE_LLM_API_KEY",
            "models": {
              "internal-reasoning-model": {
                "contextWindow": 200000,
                "thinking": true,
                "thinkingEfforts": ["low", "medium", "high"]
              }
            }
          }
        }
      crystal-version: v0.0.1
      integration-version: v0.0.1
```

Crystal Code supports the provider protocols available in its release, including DeepSeek, OpenAI-compatible Chat Completions, OpenAI Responses, Anthropic Messages, Gemini, and Ollama. The integration does not fetch model metadata. Add every selectable model to the provider definition, including its positive `contextWindow`.

The API key must stay in a GitHub Secret and an environment variable. Do not put a literal key in YAML, `config`, or `providers`.

Use native JSON for the wider configuration surface:

```yaml
with:
  task: issue-check
  provider: gateway
  model: example-model
  config: |
    {"retry":{"maximumRetries":2},"microCompaction":{"enabled":true}}
  providers: |
    {"gateway":{"protocol":"openai","baseUri":"https://example.com/v1/","apiKeyEnvironment":"GATEWAY_API_KEY","models":{"example-model":{"contextWindow":200000}}}}
```

Pass `GATEWAY_API_KEY` through the step's `env`. `config` is the native `config.json` object and `providers` is the native `providers.json` object. Explicit `provider`, `model`, and `approval` inputs override the corresponding config fields. Other JSON fields are preserved and validated by Crystal Code at startup; the integration validates JSON structure and rejects inline API keys, allowing only `{env:NAME}` references. Legacy `config.providers` is rejected. Skills, plugins, workspace trust, and repository rules are disabled for this integration; the packaged external tools are enabled.

### Configuration

The Action writes two native Crystal Code files in its temporary home:

```text
config     → config.json
providers  → providers.json
```

Use `config` for host behavior and `providers` for provider/model definitions. Common `config` fields include:

| Field | Purpose |
| :--- | :--- |
| `provider` | Active provider name |
| `model` | Active model id |
| `approval` | `default`, `edit`, `review`, `audit`, or `full` |
| `thinkingEffort` | `default`, `off`, `minimal`, `low`, `medium`, `high`, `maximum`, or a provider token |
| `approvalModel` | Optional reviewer object with `enabled`, `provider`, `model`, and `thinkingEffort` |
| `compactionModel` | Optional summarizer object with `enabled`, `provider`, `model`, and `thinkingEffort` |
| `microCompaction` | Optional object controlling removal of old tool results |
| `retry` | Retry policy, including `maximumRetries`, delays, backoff, and jitter |
| `approvalRetry` | Retry policy for review/audit approval calls |
| `verboseThinking` | Whether thinking panels are shown in interactive output; it does not change model reasoning |

For example:

```yaml
with:
  task: issue-check
  config: |
    {
      "approval": "review",
      "thinkingEffort": "high",
      "microCompaction": {
        "enabled": true,
        "protectTurns": 2,
        "protectTokens": 40000,
        "minimumTokens": 20000
      },
      "retry": {
        "enabled": true,
        "maximumRetries": 3,
        "initialDelaySeconds": 2,
        "backoffFactor": 2,
        "jitterFactor": 0.25,
        "maximumDelaySeconds": 30
      }
    }
```

Action inputs override the same effective settings where applicable:

| Input | Default | Meaning |
| :--- | :--- | :--- |
| `task` | `pull-request-check` | `pull-request-check`, `issue-check`, `issue-label`, or `custom` |
| `provider` | `deepseek` | Active provider override |
| `model` | `deepseek-flash` for DeepSeek | Active model override |
| `approval` | `review` | Approval mode override |
| `duration` | `600` | Maximum turn duration in seconds |
| `model-calls` | `32` | Maximum model calls |
| `tool-calls` | `128` | Maximum tool calls |
| `bash-timeout` | `60` | Maximum foreground shell duration |
| `prompt` | empty | Additional trusted maintainer instructions |

The execution-limit inputs are positive integers. They are passed as process-level limits and are not written into the native config object. `custom` requires a non-empty `prompt`; never interpolate issue, PR, or comment text into it.

### Keep model configuration private

Workflow files are repository content. If provider URLs, model ids, routing policy, or other model settings are confidential, store the JSON as repository or environment Secrets:

```text
CRYSTAL_CONFIG_JSON
CRYSTAL_PROVIDERS_JSON
PRIVATE_LLM_API_KEY
```

Then reference them without printing them:

```yaml
steps:
  - uses: YELANDAOKONG/CrystalCode.Integrations.GitHub@v0.0.1
    env:
      GITHUB_TOKEN: ${{ github.token }}
      PRIVATE_LLM_API_KEY: ${{ secrets.PRIVATE_LLM_API_KEY }}
    with:
      task: issue-check
      config: ${{ secrets.CRYSTAL_CONFIG_JSON }}
      providers: ${{ secrets.CRYSTAL_PROVIDERS_JSON }}
      crystal-version: v0.0.1
      integration-version: v0.0.1
```

The secret JSON can contain the active provider and model:

```json
{
  "provider": "private-gateway",
  "model": "internal-reasoning-model",
  "retry": { "maximumRetries": 2 }
}
```

The provider Secret can refer to the separately injected key:

```json
{
  "private-gateway": {
    "protocol": "openai",
    "baseUri": "https://llm.example.com/v1/",
    "apiKeyEnvironment": "PRIVATE_LLM_API_KEY",
    "models": {
      "internal-reasoning-model": { "contextWindow": 200000 }
    }
  }
}
```

The bootstrap rejects inline values in fields named `apiKey`. Do not use `echo`, shell tracing, debug actions, or third-party actions that could expose Secret values. GitHub does not provide repository Secrets to ordinary fork pull-request workflows, which is why the PR template skips fork PRs.

### Permissions and task behavior

Use `prompt` for additional trusted task instructions or classification criteria. Do not interpolate issue bodies, PR descriptions, or comments into that input; fetch them through tools as data. The packaged `github-review-guard` is loaded along with the selected task attachment. `custom` loads the guard without a business attachment. Prompts use the native `prompt-attachments/` format, not config fields. Future custom prompt packages can build on that format; arbitrary prompt directory installation is not exposed by this first Action.

Execution limits are separate inputs: `duration` (600 seconds), `model-calls` (32), `tool-calls` (128), and `bash-timeout` (60 seconds). They must be positive finite integers and override the corresponding saved run settings. Crystal Code's exit status is returned to GitHub Actions. No sessions or logs are uploaded as artifacts by the consumer templates.

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
