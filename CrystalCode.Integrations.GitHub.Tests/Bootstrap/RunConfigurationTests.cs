using System.Text.Json.Nodes;

using CrystalCode.Integrations.GitHub.Bootstrap;

namespace CrystalCode.Integrations.GitHub.Tests.Bootstrap;

public sealed class RunConfigurationTests
{
    [Fact]
    public void Read_PreservesNativeNestedSettingsAndExplicitOverrides()
    {
        var configuration = Read(new()
        {
            ["CONFIG"] = """{"provider":"gateway","model":"first","retry":{"maximumRetries":2},"approvalModel":{"enabled":true,"provider":"other","model":"reviewer"},"bashTimeoutSeconds":null}""",
            ["PROVIDERS"] = """{"gateway":{"protocol":"responses","baseUri":"https://example.com/v1/","apiKey":"{env:GATEWAY_KEY}","models":{"second":{"contextWindow":200000,"thinking":true}}}}""",
            ["MODEL"] = "second"
        });

        Assert.Equal("gateway", configuration.Config["provider"]!.GetValue<string>());
        Assert.Equal("second", configuration.Config["model"]!.GetValue<string>());
        Assert.Equal(2, configuration.Config["retry"]!["maximumRetries"]!.GetValue<int>());
        Assert.Equal("reviewer", configuration.Config["approvalModel"]!["model"]!.GetValue<string>());
        Assert.Null(configuration.Config["bashTimeoutSeconds"]);
        Assert.Equal(200000, configuration.Providers["gateway"]!["models"]!["second"]!["contextWindow"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("{\"apiKey\":\"synthetic-secret\"}")]
    [InlineData("{\"nested\":{\"APIKEY\":\"synthetic-secret\"}}")]
    [InlineData("{\"apiKey\":\"{file:credentials}\"}")]
    public void Read_RejectsSecretValuesWithoutEchoingThem(string json)
    {
        foreach (var input in new[] { "CONFIG", "PROVIDERS" })
        {
            var exception = Assert.Throws<InvalidOperationException>(() => Read(new() { [input] = json }));
            Assert.DoesNotContain("synthetic-secret", exception.Message);
        }
    }

    [Theory]
    [InlineData("{\"provider\":")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"provider\":\"first\",\"provider\":\"second\"}")]
    [InlineData("{\"provider\":\"first\",\"Provider\":\"second\"}")]
    [InlineData("{\"Providers\":{}}")]
    public void Read_RejectsMalformedOrAmbiguousConfig(string json) =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["CONFIG"] = json }));

    [Fact]
    public void Read_DoesNotGuessModelForCustomProvider() =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["PROVIDER"] = "gateway" }));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("unlimited")]
    [InlineData("$(touch unexpected)")]
    public void Read_RequiresFinitePositiveBudgets(string value) =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["DURATION"] = value }));

    [Theory]
    [InlineData("pull-request-check", "--plan", "github-pr-check")]
    [InlineData("issue-check", "--plan", "github-issue-check")]
    [InlineData("issue-label", "--work", "github-issue-label")]
    [InlineData("issue-reply", "--work", "github-issue-reply")]
    [InlineData("pull-request-reply", "--work", "github-pull-request-reply")]
    public void CreateStartInfo_SelectsCatalogAndAttachments(string task, string mode, string attachment)
    {
        var configuration = Read(new() { ["TASK"] = task, ["PROMPT"] = "Literal $(shell) `text`\nnext line" });
        var start = configuration.CreateStartInfo("CrystalCode", "home with spaces", "workspace");

        Assert.False(start.UseShellExecute);
        Assert.True(start.RedirectStandardInput);
        Assert.Contains(mode, start.ArgumentList);
        Assert.Contains("github-review-guard", start.ArgumentList);
        Assert.Contains(attachment, start.ArgumentList);
        Assert.DoesNotContain(configuration.Prompt, start.ArgumentList);
        Assert.DoesNotContain("Literal $(shell)", string.Join(' ', start.ArgumentList));
        Assert.Contains("home with spaces", start.ArgumentList);
        Assert.Equal("home with spaces", start.Environment["CRYSTAL_HOME"]);
        Assert.Equal("off", start.ArgumentList[start.ArgumentList.IndexOf("--rules") + 1]);
        Assert.Equal("on", start.ArgumentList[start.ArgumentList.IndexOf("--prompt-attachments") + 1]);
        Assert.Contains("Literal $(shell) `text`\nnext line", configuration.Prompt);
    }

    [Fact]
    public void Read_LoadsTrustedHomeAndExtensionSwitches()
    {
        var root = Path.Combine(Path.GetTempPath(), "crystal-github-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "config.json"), "{\"provider\":\"gateway\",\"model\":\"private\",\"retry\":{\"maximumRetries\":2}}");
            File.WriteAllText(Path.Combine(root, "providers.json"), "{\"gateway\":{\"models\":{\"private\":{\"contextWindow\":200000}}}}");
            var configuration = Read(new()
            {
                ["HOME_DIRECTORY"] = root,
                ["PROMPT_SET"] = "maintainer",
                ["PROMPT_ATTACHMENT"] = "first\nsecond",
                ["PLUGINS"] = "on",
                ["SKILLS"] = "on",
                ["EXTERNAL_TOOLS"] = "off",
                ["TASK"] = "custom",
                ["PROMPT"] = "Use the trusted task.",
                ["MODE"] = "plan"
            });

            var start = configuration.CreateStartInfo("CrystalCode", "home", "workspace");
            Assert.Equal("gateway", configuration.Config["provider"]!.GetValue<string>());
            Assert.Equal(2, configuration.Config["retry"]!["maximumRetries"]!.GetValue<int>());
            Assert.Contains("--plugins", start.ArgumentList);
            Assert.Equal("on", start.ArgumentList[start.ArgumentList.IndexOf("--plugins") + 1]);
            Assert.Equal("maintainer", start.ArgumentList[start.ArgumentList.IndexOf("--prompt-set") + 1]);
            Assert.Contains("first", start.ArgumentList);
            Assert.Contains("second", start.ArgumentList);
            Assert.Contains("--plan", start.ArgumentList);
            Assert.Equal("off", start.ArgumentList[start.ArgumentList.IndexOf("--external-tools") + 1]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Read_CustomTaskRequiresWorkflowInstructions() =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["TASK"] = "custom" }));

    [Theory]
    [InlineData("issue-label", "{\"issue\":{\"number\":12,\"pull_request\":{}}}")]
    [InlineData("issue-label", "{\"inputs\":{\"issue\":12}}")]
    [InlineData("issue-check", "{\"issue\":{\"number\":0}}")]
    [InlineData("pull-request-check", "{\"issue\":{\"number\":12}}")]
    [InlineData("pull-request-reply", "{\"issue\":{\"number\":12}}")]
    public void ValidateEvent_RefusesMismatchedTarget(string task, string json) =>
        Assert.Throws<InvalidOperationException>(() => Read(new() { ["TASK"] = task }).ValidateEvent(json));

    [Theory]
    [InlineData("issue-label", "{\"issue\":{\"number\":12}}")]
    [InlineData("issue-check", "{\"issue\":{\"number\":12}}")]
    [InlineData("pull-request-check", "{\"pull_request\":{\"number\":12}}")]
    [InlineData("pull-request-reply", "{\"pull_request\":{\"number\":12}}")]
    public void ValidateEvent_AcceptsMatchingTarget(string task, string json) =>
        Read(new() { ["TASK"] = task }).ValidateEvent(json);

    [Fact]
    public void WriteHome_PreservesJsonAndDoesNotCreateCredentialFile()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = Read(new() { ["CONFIG"] = """{"microCompaction":{"enabled":true},"skills":true}""" });
            configuration.WriteHome(root);
            var written = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "config.json")))!;

            Assert.True(written["microCompaction"]!["enabled"]!.GetValue<bool>());
            Assert.False(written["skills"]!.GetValue<bool>());
            Assert.False(File.Exists(Path.Combine(root, "credentials.json")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(root, "providers.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static RunConfiguration Read(Dictionary<string, string?> values)
    {
        var trustedHome = values.GetValueOrDefault("HOME_DIRECTORY");
        return RunConfiguration.Read(
            key => values.GetValueOrDefault(key.Replace("CRYSTAL_GITHUB_", "", StringComparison.Ordinal)), trustedHome);
    }
}
