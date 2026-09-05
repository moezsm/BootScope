using Microsoft.Win32;
using System.IO;

namespace BootScope.Services;

/// <summary>
/// Detects which executables are registered to launch automatically when Windows starts, by
/// reading the standard Run/RunOnce registry keys (current user and local machine) and the
/// Startup shell folders. This is read-only: BootScope never modifies these locations.
/// </summary>
public class StartupDetectionService
{
    private static readonly string[] RunKeyPaths =
    {
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
    };

    /// <summary>
    /// Returns the set of executable file paths (lower-cased, normalized) that are registered
    /// to start automatically with Windows for the current user and machine.
    /// </summary>
    public HashSet<string> GetStartupExecutablePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            CollectFromRegistry(Registry.CurrentUser, paths);
            CollectFromRegistry(Registry.LocalMachine, paths);
            CollectFromStartupFolder(Environment.SpecialFolder.Startup, paths);
            CollectFromStartupFolder(Environment.SpecialFolder.CommonStartup, paths);
        }
        catch (Exception)
        {
            // Best-effort: if the registry or file system can't be read (e.g. restricted
            // environment), simply report no known startup items rather than crashing.
        }

        return paths;
    }

    private static void CollectFromRegistry(RegistryKey root, HashSet<string> paths)
    {
        foreach (var keyPath in RunKeyPaths)
        {
            try
            {
                using var key = root.OpenSubKey(keyPath);
                if (key is null)
                {
                    continue;
                }

                foreach (var valueName in key.GetValueNames())
                {
                    if (key.GetValue(valueName) is string command)
                    {
                        var exePath = ExtractExecutablePath(command);
                        if (!string.IsNullOrWhiteSpace(exePath))
                        {
                            paths.Add(exePath);
                        }
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
                // Access denied reading this key; skip it gracefully.
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied reading this key; skip it gracefully.
            }
        }
    }

    private static void CollectFromStartupFolder(Environment.SpecialFolder folder, HashSet<string> paths)
    {
        try
        {
            var folderPath = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                paths.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip folders we can't read.
        }
        catch (IOException)
        {
            // Skip folders that raise transient IO errors.
        }
    }

    /// <summary>
    /// Registry Run values often look like: "C:\Path\App.exe" /flag or C:\Path\App.exe.
    /// Extracts just the executable path.
    /// </summary>
    private static string ExtractExecutablePath(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', 1);
            return closingQuote > 0 ? command[1..closingQuote] : command;
        }

        var spaceIndex = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return spaceIndex > 0 ? command[..(spaceIndex + 4)] : command;
    }
}
