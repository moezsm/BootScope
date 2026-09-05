namespace BootScope.Models;

/// <summary>
/// A single entry in the startup session log, capturing the top resource-consuming processes
/// shortly after the user logs in so they can review what slowed down their machine.
/// </summary>
public class SessionLogEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public string ProcessName { get; init; } = string.Empty;

    public int ProcessId { get; init; }

    public double CpuUsagePercent { get; init; }

    public double MemoryUsageMb { get; init; }

    /// <summary>Which metric triggered this entry being logged ("CPU" or "RAM").</summary>
    public string Reason { get; init; } = string.Empty;
}
