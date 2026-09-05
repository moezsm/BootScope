using System.IO;
using System.Text.Json;
using BootScope.Models;

namespace BootScope.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under the current user's local
/// application data folder (no admin rights required).
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BootScope");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt or unreadable settings file: fall back to defaults rather than crash.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception)
        {
            // Best-effort persistence; a failure to save settings should not crash the app.
        }
    }
}
