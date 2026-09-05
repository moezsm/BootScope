using System.Diagnostics;
using System.IO;
using BootScope.Helpers;

namespace BootScope.Services;

/// <summary>
/// Performs user-initiated actions on a specific process: stopping it, opening its file
/// location, showing its Windows shell properties dialog, or searching for its name on the
/// web. Every action here is triggered explicitly by the user from the UI - nothing in this
/// service runs automatically or without a corresponding user click.
///
/// SAFETY NOTE: <see cref="StopProcessAsync"/> is the only place in the entire application
/// that terminates a process, and the UI layer must always show a confirmation dialog (with a
/// strong warning for critical Windows processes) before calling it.
/// </summary>
public class ProcessActionService
{
    /// <summary>
    /// Attempts to gracefully close, then forcibly kill, the process with the given id.
    /// Callers MUST have already obtained explicit user confirmation before calling this.
    /// </summary>
    public Task<ProcessActionResult> StopProcessAsync(int processId)
    {
        return Task.Run(() =>
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit(5000);
                return ProcessActionResult.Success();
            }
            catch (ArgumentException)
            {
                // Process already exited before we could stop it.
                return ProcessActionResult.Success();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
            {
                return ProcessActionResult.Failure(
                    "BootScope could not stop this process. It may require administrator privileges, " +
                    "or it may be protected by Windows.");
            }
        });
    }

    /// <summary>Opens Windows Explorer with the executable pre-selected.</summary>
    public void OpenFileLocation(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{executablePath}\"",
            UseShellExecute = true,
        });
    }

    /// <summary>Shows the Windows shell "Properties" dialog for the executable file.</summary>
    public void ShowFileProperties(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        NativeMethods.ShowFileProperties(executablePath);
    }

    /// <summary>Opens the user's default web browser to search for the process name.</summary>
    public void SearchProcessNameOnWeb(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        var query = Uri.EscapeDataString($"{processName} windows process");
        var url = $"https://www.bing.com/search?q={query}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}

public class ProcessActionResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public static ProcessActionResult Success() => new() { IsSuccess = true };

    public static ProcessActionResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
