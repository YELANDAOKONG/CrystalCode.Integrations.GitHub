using System.Collections;

using CrystalCode.Integrations.GitHub;

namespace CrystalCode.Integrations.GitHub.Command;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
            {
                environment[key] = entry.Value as string;
            }
        }

        return await GitHubCommand
            .RunAsync(args, environment, GitHubClient.CreateHttp, Console.Out, Console.Error, CancellationToken.None)
            .ConfigureAwait(false);
    }
}
