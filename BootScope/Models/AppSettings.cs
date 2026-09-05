namespace BootScope.Models;

/// <summary>
/// User-configurable settings, persisted as JSON in the user's local application data folder.
/// </summary>
public class AppSettings
{
    /// <summary>Whether BootScope should launch automatically at Windows login.</summary>
    public bool LaunchAtLogin { get; set; } = true;

    /// <summary>
    /// Seconds to wait after login before starting the startup scan, so BootScope itself does
    /// not add to boot-time load. Defaults to 45 seconds per product requirements.
    /// </summary>
    public int StartupScanDelaySeconds { get; set; } = 45;

    /// <summary>How often (in seconds) the live process list and dashboard refresh.</summary>
    public int RefreshIntervalSeconds { get; set; } = 2;

    /// <summary>CPU usage percentage above which a process row is highlighted as high usage.</summary>
    public double CpuWarningThresholdPercent { get; set; } = 25.0;

    /// <summary>RAM usage percentage (of total physical memory) above which a process is highlighted.</summary>
    public double RamWarningThresholdPercent { get; set; } = 10.0;
}
