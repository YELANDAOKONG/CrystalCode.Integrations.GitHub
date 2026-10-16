using System.Text.Json;

using Crystal.Tools;

namespace CrystalCode.Integrations.GitHub.Tools;

internal static class GitHubArguments
{
    internal static JsonElement Object(ToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        using var document = JsonDocument.Parse(call.Arguments);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Tool arguments must be a JSON object.");
        }

        return document.RootElement.Clone();
    }

    internal static int Number(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("number", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var number)
            || number < 1)
        {
            throw new GitHubException("Issue and pull request numbers start at 1.");
        }

        return number;
    }

    internal static string Required(JsonElement arguments, string name)
    {
        var text = Optional(arguments, name);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new GitHubException($"Missing {name}.");
        }

        return text;
    }

    internal static string? Optional(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new GitHubException($"{name} must be text.");
        }

        return value.GetString();
    }

    internal static IReadOnlyList<string> Strings(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new GitHubException($"Missing {name}.");
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString()))
            {
                throw new GitHubException($"{name} must be text.");
            }

            items.Add(item.GetString()!);
        }

        return items;
    }
}
