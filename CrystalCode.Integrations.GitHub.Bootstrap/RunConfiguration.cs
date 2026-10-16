using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrystalCode.Integrations.GitHub.Bootstrap;

public sealed class RunConfiguration
{
    private const string Prefix = "CRYSTAL_GITHUB_";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private RunConfiguration(JsonObject config, JsonObject providers, string profile, string prompt,
        string duration, string modelCalls, string toolCalls, string bashTimeout)
    {
        Config = config;
        Providers = providers;
        Profile = profile;
        Prompt = prompt;
        Duration = duration;
        ModelCalls = modelCalls;
        ToolCalls = toolCalls;
        BashTimeout = bashTimeout;
    }

    public JsonObject Config { get; }
    public JsonObject Providers { get; }
    public string Profile { get; }
    public string Prompt { get; }
    public string Duration { get; }
    public string ModelCalls { get; }
    public string ToolCalls { get; }
    public string BashTimeout { get; }

    public static RunConfiguration Read(Func<string, string?> environment)
    {
        string Value(string name, string fallback = "") => environment(Prefix + name) is { Length: > 0 } value
            ? value : fallback;

        var config = ParseObject(Value("CONFIG", "{}"), "config");
        var providers = ParseObject(Value("PROVIDERS", "{}"), "providers");
        if (config.ContainsKey("providers"))
        {
            throw new InvalidOperationException("Use the providers input instead of config.providers.");
        }

        ValidateSecrets(config);
        ValidateSecrets(providers);
        foreach (var key in new[] { "provider", "model", "approval" })
        {
            var value = Value(key.ToUpperInvariant());
            if (value.Length > 0)
            {
                config[key] = value;
            }
        }

        config["provider"] ??= "deepseek";
        if (config["model"] is null && config["provider"]?.ToString() == "deepseek")
        {
            config["model"] = "deepseek-flash";
        }
        config["approval"] ??= "review";
        if (config["provider"] is not JsonValue provider || !provider.TryGetValue<string>(out var providerName)
            || !Regex.IsMatch(providerName, "^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Provider must be a provider name.");
        }

        if (config["model"] is not JsonValue model || !model.TryGetValue<string>(out var modelName)
            || string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("Set a non-empty model for the selected provider.");
        }

        if (config["approval"]?.ToString() is not ("default" or "edit" or "review" or "audit" or "full"))
        {
            throw new InvalidOperationException("Approval mode is invalid.");
        }

        // The integration needs only its installed tools; repository extensions are not loaded.
        config["externalTools"] = true;
        config["skills"] = false;
        config["plugins"] = false;
        var profile = Value("TASK", "pull-request-check");
        var task = profile switch
        {
            "pull-request-check" => "Inspect the current GitHub pull request. Read the event and PR diff with the GitHub tools. Report actionable defects with file and line evidence. Output the report only; do not publish or change GitHub state.",
            "issue-check" => "Inspect the current GitHub issue using the event and GitHub tools. Explain missing information, reproducibility, and useful next steps. Output the report only; do not publish or change GitHub state.",
            "issue-label" => "Classify the current GitHub issue using the event and GitHub tools. List existing repository labels with a GET request first. Add only applicable existing labels using github_issue_labels. Do not remove labels, create labels, comment, or change any other GitHub state.",
            "custom" => "Perform only the task specified in the workflow instructions below.",
            _ => throw new InvalidOperationException("Task must be pull-request-check, issue-check, issue-label, or custom.")
        };
        var prompt = Value("PROMPT");
        if (profile == "custom" && string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("The custom task requires a prompt.");
        }

        if (prompt.Length > 0)
        {
            task += "\n\nAdditional instructions supplied by the workflow maintainer:\n" + prompt;
        }

        return new RunConfiguration(config, providers, profile, task,
            Positive(Value("DURATION", "600"), "duration"),
            Positive(Value("MODEL_CALLS", "32"), "model-calls"),
            Positive(Value("TOOL_CALLS", "128"), "tool-calls"),
            Positive(Value("BASH_TIMEOUT", "60"), "bash-timeout"));
    }

    public void WriteHome(string home)
    {
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "config.json"), Config.ToJsonString(JsonOptions));
        File.WriteAllText(Path.Combine(home, "providers.json"), Providers.ToJsonString(JsonOptions));
    }

    public void ValidateEvent(string json)
    {
        if (Profile == "custom")
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var member = Profile == "pull-request-check" ? "pull_request" : "issue";
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(member, out var target)
                || target.ValueKind != JsonValueKind.Object
                || !target.TryGetProperty("number", out var number)
                || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var value) || value <= 0
                || (member == "issue" && target.TryGetProperty("pull_request", out _)))
            {
                throw new InvalidOperationException("The GitHub event does not contain a target matching the selected task.");
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("The GitHub event is not valid JSON.");
        }
    }

    public ProcessStartInfo CreateStartInfo(string executable, string home, string workspace)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            WorkingDirectory = workspace
        };
        foreach (var argument in new[]
        {
            "run", "--home", home, "--workspace", workspace, "--workspace-trust", "off",
            "--duration", Duration, "--model-calls", ModelCalls, "--tool-calls", ToolCalls,
            "--bash-timeout", BashTimeout, "--skills", "off", "--plugins", "off",
            "--external-tools", "on", "--rules", "off", "--prompt-set", "default", "--prompt-attachments", "on",
            "--prompt-attachment", "github-review-guard",
            Profile is "pull-request-check" or "issue-check" ? "--plan" : "--work"
        })
        {
            start.ArgumentList.Add(argument);
        }

        if (Profile != "custom")
        {
            start.ArgumentList.Add("--prompt-attachment");
            start.ArgumentList.Add(Profile switch
            {
                "pull-request-check" => "github-pr-check",
                "issue-check" => "github-issue-check",
                "issue-label" => "github-issue-label",
                _ => throw new InvalidOperationException("Unknown task profile.")
            });
        }

        start.Environment["CRYSTAL_HOME"] = home;
        // JSON inputs may contain long prompts; only credentials and runtime environment need inheritance.
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
        {
            start.Environment.Remove(key);
        }

        return start;
    }

    private static JsonObject ParseObject(string json, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            ValidateDuplicateKeys(document.RootElement);
            var result = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException($"The {name} input must be a JSON object.");
            if (name != "config")
            {
                return result;
            }

            if (result.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
            {
                throw new InvalidOperationException("The config input contains ambiguous field names.");
            }

            return new JsonObject(result.Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())),
                new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            // Parser diagnostics can echo secret-bearing input.
            throw new InvalidOperationException($"The {name} input contains invalid JSON.");
        }
    }

    private static void ValidateDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name))
                {
                    throw new InvalidOperationException("JSON inputs must not contain duplicate fields.");
                }

                ValidateDuplicateKeys(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateDuplicateKeys(item);
            }
        }
    }

    private static void ValidateSecrets(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                if (key.Equals("apiKey", StringComparison.OrdinalIgnoreCase)
                    && value is not null
                    && !Regex.IsMatch(value.ToString(), "^\\{env:[A-Za-z_][A-Za-z0-9_]*\\}$", RegexOptions.CultureInvariant))
                {
                    throw new InvalidOperationException("API keys must be supplied through environment variables; only {env:NAME} references are allowed in JSON.");
                }

                ValidateSecrets(value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                ValidateSecrets(child);
            }
        }
    }

    private static string Positive(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
        {
            throw new InvalidOperationException($"The {name} input must be a positive integer.");
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }
}
