using System.Diagnostics;
using System.Collections;

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
            var environment = ReadEnvironment();
            var trustedHome = ResolveTrustedDirectory(environment);
            if (environment.TryGetValue("CRYSTAL_GITHUB_PROMPT_FILE", out var promptFile)
                && !string.IsNullOrWhiteSpace(promptFile))
            {
                if (environment.TryGetValue("CRYSTAL_GITHUB_PROMPT", out var prompt) && !string.IsNullOrWhiteSpace(prompt))
                {
                    throw new InvalidOperationException("Use prompt or prompt-file, not both.");
                }

                environment["CRYSTAL_GITHUB_PROMPT"] = ReadTrustedText(promptFile);
            }

            var configuration = RunConfiguration.Read(name => environment.GetValueOrDefault(name), trustedHome);
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
            if (trustedHome is not null)
            {
                CopyTrustedHome(trustedHome, home);
            }
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

    private static Dictionary<string, string?> ReadEnvironment()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            values[(string)entry.Key] = entry.Value?.ToString();
        }

        return values;
    }

    private static string? ResolveTrustedDirectory(IReadOnlyDictionary<string, string?> environment)
    {
        if (!environment.TryGetValue("CRYSTAL_GITHUB_HOME_DIRECTORY", out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var path = Path.GetFullPath(value, Environment.CurrentDirectory);
        if (!Directory.Exists(path))
        {
            throw new InvalidOperationException("The trusted home directory does not exist.");
        }

        ValidateTrustedTree(path);
        return path;
    }

    private static string ReadTrustedText(string value)
    {
        var path = Path.GetFullPath(value, Environment.CurrentDirectory);
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The prompt file must be an existing regular file.");
        }

        const long maximumPromptBytes = 1024 * 1024;
        if (new FileInfo(path).Length > maximumPromptBytes)
        {
            throw new InvalidOperationException("The prompt file is too large.");
        }

        return File.ReadAllText(path);
    }

    private static void ValidateTrustedTree(string path)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Trusted configuration must not contain symbolic links.");
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                ValidateTrustedTree(entry);
            }
        }
    }

    private static void CopyTrustedHome(string source, string destination)
    {
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "config.json", "providers.json" };
        var allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "prompt-sets", "prompt-attachments", "plugins", "skills", "tools"
        };

        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var name = Path.GetFileName(entry);
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if (!allowedDirectories.Contains(name))
                {
                    throw new InvalidOperationException($"Unsupported trusted home directory entry: {name}.");
                }

                CopyOverlayDirectory(entry, Path.Combine(destination, name));
            }
            else if (!allowedFiles.Contains(name))
            {
                throw new InvalidOperationException($"Unsupported trusted home file: {name}.");
            }
        }
    }

    private static void CopyOverlayDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Trusted configuration must not contain symbolic links.");
            }

            var target = Path.Combine(destination, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyOverlayDirectory(entry, target);
            }
            else
            {
                if (File.Exists(target))
                {
                    throw new InvalidOperationException($"Trusted home entry conflicts with an installed asset: {Path.GetFileName(entry)}.");
                }

                File.Copy(entry, target);
            }
        }
    }
}
