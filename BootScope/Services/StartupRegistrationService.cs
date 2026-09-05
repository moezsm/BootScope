using Microsoft.Win32;

namespace BootScope.Services;

/// <summary>
/// Enables or disables launching BootScope automatically when the current user logs into
/// Windows, using the standard per-user Run registry key. This only ever affects the current
/// user's account (HKCU) and never requires administrator privileges.
/// </summary>
public class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BootScope";

    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void SetLaunchAtLogin(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "BootScope.exe");
                key?.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // If we can't write to HKCU (very unusual), silently leave the current state
            // unchanged rather than crash the Settings page.
        }
    }
}
