using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BootScope.Models;

namespace BootScope.Services;

/// <summary>
/// Records a simple session log of the top CPU and RAM consuming processes observed during
/// the startup scan, so the user can review what was heavy on this boot later. This is purely
/// informational logging - it never acts on the processes it records.
/// </summary>
public class SessionLogService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BootScope", "Logs");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes a startup session log entry file containing the top CPU and RAM consumers.
    /// </summary>
    public void RecordStartupSession(IReadOnlyList<ProcessInfo> topCpuProcesses, IReadOnlyList<ProcessInfo> topMemoryProcesses)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            var entries = new List<SessionLogEntry>();
            entries.AddRange(topCpuProcesses.Select(p => new SessionLogEntry
            {
                ProcessName = p.Name,
                ProcessId = p.ProcessId,
                CpuUsagePercent = p.CpuUsagePercent,
                MemoryUsageMb = p.MemoryUsageMb,
                Reason = SessionLogReason.Cpu,
            }));
            entries.AddRange(topMemoryProcesses.Select(p => new SessionLogEntry
            {
                ProcessName = p.Name,
                ProcessId = p.ProcessId,
                CpuUsagePercent = p.CpuUsagePercent,
                MemoryUsageMb = p.MemoryUsageMb,
                Reason = SessionLogReason.Ram,
            }));

            var fileName = $"session-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(LogDirectory, fileName);
            File.WriteAllText(filePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception)
        {
            // Session logging is a diagnostic convenience, not a critical feature; a failure
            // to write the log should never interrupt the startup scan or the UI.
        }
    }

    /// <summary>Returns the folder where session log files are stored, creating it if needed.</summary>
    public string GetLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);
        return LogDirectory;
    }
}
