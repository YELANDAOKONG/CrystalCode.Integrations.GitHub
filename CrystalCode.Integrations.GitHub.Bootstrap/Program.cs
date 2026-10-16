using System.Diagnostics;

namespace CrystalCode.Integrations.GitHub.Bootstrap;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: CrystalCode.Integrations.GitHub.Bootstrap <CrystalCode executable> <assets directory> <temporary directory>");
            return 1;
        }

        try
        {
            var configuration = RunConfiguration.Read(Environment.GetEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")))
            {
                throw new InvalidOperationException("GITHUB_TOKEN and GITHUB_REPOSITORY are required.");
            }

            var eventPath = Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH");
            if (string.IsNullOrWhiteSpace(eventPath)
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME")))
            {
                throw new InvalidOperationException("GITHUB_EVENT_PATH and GITHUB_EVENT_NAME are required.");
            }

            configuration.ValidateEvent(await File.ReadAllTextAsync(eventPath));
            var executable = Path.GetFullPath(args[0]);
            var assets = Path.GetFullPath(args[1]);
            var root = Path.Combine(Path.GetFullPath(args[2]), "run-" + Guid.NewGuid().ToString("N"));
            var home = Path.Combine(root, "home");
            var workspace = Path.Combine(root, "workspace");
            Directory.CreateDirectory(workspace);
            configuration.WriteHome(home);
            CopyDirectory(Path.Combine(assets, "tools"), Path.Combine(home, "tools"));
            CopyDirectory(Path.Combine(assets, "prompt-attachments"), Path.Combine(home, "prompt-attachments"));
            if (!File.Exists(Path.Combine(home, "tools", "GitHub", "tools.json"))
                || !File.Exists(Path.Combine(home, "tools", "GitHub", "CrystalCode.Integrations.GitHub.Tools.dll")))
            {
                throw new InvalidOperationException("The GitHub tools are missing from the integration package.");
            }

            using var process = Process.Start(configuration.CreateStartInfo(executable, home, workspace))
                ?? throw new InvalidOperationException("Could not start Crystal Code.");
            await process.StandardInput.WriteAsync(configuration.Prompt);
            process.StandardInput.Close();
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or ArgumentException)
        {
            Console.Error.WriteLine(exception is InvalidOperationException
                ? exception.Message : "Could not assemble or run the integration package.");
            return 1;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Integration assets must not contain symbolic links.");
            }

            var target = Path.Combine(destination, Path.GetFileName(path));
            if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyDirectory(path, target);
            }
            else
            {
                File.Copy(path, target);
            }
        }
    }
}
