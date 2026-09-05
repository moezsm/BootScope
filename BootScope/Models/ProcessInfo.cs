using BootScope.Helpers;

namespace BootScope.Models;

/// <summary>
/// Represents a single running process and everything BootScope knows about it. Instances are
/// refreshed in place on every polling tick so that the bound <see cref="System.Windows.Data.CollectionView"/>
/// keeps its sort/filter/selection state stable across refreshes.
/// </summary>
public class ProcessInfo : ObservableObject
{
    private double _cpuUsagePercent;
    private double _memoryUsageMb;
    private double _memoryUsagePercent;
    private double _diskUsageMbPerSec;
    private double _networkUsageKbPerSec;
    private bool _isSelected;

    /// <summary>Operating-system process id. Stable identity key for the process while it is running.</summary>
    public int ProcessId { get; init; }

    /// <summary>Executable/image name, e.g. "explorer.exe".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Percentage of total CPU capacity currently used by this process (0-100+ on multi-core).</summary>
    public double CpuUsagePercent
    {
        get => _cpuUsagePercent;
        set => SetProperty(ref _cpuUsagePercent, value);
    }

    /// <summary>Working set memory usage in megabytes.</summary>
    public double MemoryUsageMb
    {
        get => _memoryUsageMb;
        set => SetProperty(ref _memoryUsageMb, value);
    }

    /// <summary>Working set memory usage as a percentage of total installed physical RAM.</summary>
    public double MemoryUsagePercent
    {
        get => _memoryUsagePercent;
        set => SetProperty(ref _memoryUsagePercent, value);
    }

    /// <summary>Approximate disk I/O throughput for the process in MB/sec (read + write).</summary>
    public double DiskUsageMbPerSec
    {
        get => _diskUsageMbPerSec;
        set => SetProperty(ref _diskUsageMbPerSec, value);
    }

    /// <summary>
    /// Approximate network throughput for the process in KB/sec. Per-process network usage is
    /// not directly exposed by Win32/WMI without ETW tracing, so this may be 0/"N/A" when it
    /// cannot be measured; it is never fabricated.
    /// </summary>
    public double NetworkUsageKbPerSec
    {
        get => _networkUsageKbPerSec;
        set => SetProperty(ref _networkUsageKbPerSec, value);
    }

    /// <summary>Company/publisher name read from the executable's version info, if available.</summary>
    public string Publisher { get; set; } = "Unknown";

    /// <summary>Full path to the executable on disk, if it could be read.</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>True when this is a recognised Windows/Microsoft-signed process.</summary>
    public bool IsWindowsProcess { get; set; }

    /// <summary>True when this process is configured to launch automatically at Windows startup.</summary>
    public bool StartsWithWindows { get; set; }

    /// <summary>Safety classification used to warn the user before they close a process.</summary>
    public ProcessSafetyLevel SafetyLevel { get; set; } = ProcessSafetyLevel.Unknown;

    /// <summary>Guidance shown to the user; never enforced automatically.</summary>
    public ProcessRecommendation Recommendation { get; set; } = ProcessRecommendation.NeedsInvestigation;

    /// <summary>True when access to some of this process's information was denied by the OS.</summary>
    public bool HasAccessDeniedInfo { get; set; }

    /// <summary>Bound to the DataGrid's row selection so commands can act on the selected process.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Convenience flag the UI uses to highlight unusually CPU-hungry rows.</summary>
    public bool IsHighCpu { get; set; }

    /// <summary>Convenience flag the UI uses to highlight unusually RAM-hungry rows.</summary>
    public bool IsHighMemory { get; set; }
}
