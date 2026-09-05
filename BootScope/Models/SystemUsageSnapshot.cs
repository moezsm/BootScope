namespace BootScope.Models;

/// <summary>
/// A point-in-time snapshot of aggregate system resource usage, shown on the main dashboard.
/// </summary>
public class SystemUsageSnapshot
{
    public double CpuUsagePercent { get; init; }

    public double MemoryUsedMb { get; init; }

    public double MemoryTotalMb { get; init; }

    public double MemoryUsagePercent => MemoryTotalMb <= 0 ? 0 : MemoryUsedMb / MemoryTotalMb * 100.0;

    public double DiskUsagePercent { get; init; }

    public double NetworkUsageKbPerSec { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
