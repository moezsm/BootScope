namespace BootScope.Models;

/// <summary>Which resource metric caused a process to be recorded in the session log.</summary>
public enum SessionLogReason
{
    Cpu,
    Ram
}

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

    /// <summary>Which metric triggered this entry being logged.</summary>
    public SessionLogReason Reason { get; init; }
}
