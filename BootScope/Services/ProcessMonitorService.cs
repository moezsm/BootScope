using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BootScope.Helpers;
using BootScope.Models;

namespace BootScope.Services;

/// <summary>
/// Polls the operating system for the list of running processes and their resource usage.
/// All expensive work happens on background threads (via Task.Run) so the UI thread is never
/// blocked; only the final collection mutation is marshalled back to the UI thread by the
/// caller (the view model), keeping this service UI-framework agnostic.
///
/// SAFETY NOTE: this service is strictly read-only. It never starts, stops, or modifies any
/// process. Process termination lives exclusively in <see cref="ProcessActionService"/> and
/// always requires explicit user confirmation from the UI layer.
/// </summary>
public class ProcessMonitorService
{
    private readonly SafetyClassificationService _classificationService;
    private readonly StartupDetectionService _startupDetectionService;
    private readonly Dictionary<int, ProcessSample> _previousSamples = new();
    private DateTime _lastSampleTimeUtc = DateTime.UtcNow;

    public ProcessMonitorService(SafetyClassificationService classificationService, StartupDetectionService startupDetectionService)
    {
        _classificationService = classificationService;
        _startupDetectionService = startupDetectionService;
    }

    /// <summary>
    /// Captures a fresh snapshot of every running process. Safe to call repeatedly; CPU and
    /// disk usage are computed as deltas since the previous call.
    /// </summary>
    public Task<IReadOnlyList<ProcessInfo>> GetProcessSnapshotAsync(double cpuWarningThreshold, double ramWarningThreshold)
    {
        return Task.Run(() =>
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = Math.Max((now - _lastSampleTimeUtc).TotalSeconds, 0.001);
            var processorCount = Math.Max(Environment.ProcessorCount, 1);
            var totalPhysicalMemoryMb = GetTotalPhysicalMemoryMb();
            var startupPaths = _startupDetectionService.GetStartupExecutablePaths();

            var currentSamples = new Dictionary<int, ProcessSample>();
            var results = new List<ProcessInfo>();

            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    var info = BuildProcessInfo(process, elapsedSeconds, processorCount, totalPhysicalMemoryMb, startupPaths, currentSamples);
                    if (info is not null)
                    {
                        info.Recommendation = _classificationService.Recommend(
                            info.SafetyLevel, info.CpuUsagePercent, info.MemoryUsagePercent, cpuWarningThreshold, ramWarningThreshold);
                        info.IsHighCpu = info.CpuUsagePercent >= cpuWarningThreshold;
                        info.IsHighMemory = info.MemoryUsagePercent >= ramWarningThreshold;
                        results.Add(info);
                    }
                }
            }

            _previousSamples.Clear();
            foreach (var sample in currentSamples)
            {
                _previousSamples[sample.Key] = sample.Value;
            }

            _lastSampleTimeUtc = now;
            return (IReadOnlyList<ProcessInfo>)results;
        });
    }

    private ProcessInfo? BuildProcessInfo(
        Process process,
        double elapsedSeconds,
        int processorCount,
        double totalPhysicalMemoryMb,
        HashSet<string> startupPaths,
        Dictionary<int, ProcessSample> currentSamples)
    {
        try
        {
            var hasAccessDenied = false;
            string executablePath = string.Empty;
            string publisher = "Unknown";
            long workingSetBytes = 0;
            TimeSpan totalProcessorTime = TimeSpan.Zero;
            NativeMethods.IO_COUNTERS ioCounters = default;
            var hasIoCounters = false;

            try
            {
                workingSetBytes = process.WorkingSet64;
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                hasAccessDenied = true;
            }

            try
            {
                totalProcessorTime = process.TotalProcessorTime;
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                hasAccessDenied = true;
            }

            try
            {
                executablePath = process.MainModule?.FileName ?? string.Empty;
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                // Very common for elevated/system processes when BootScope runs as a normal
                // user. This is expected and handled gracefully rather than crashing.
                hasAccessDenied = true;
            }

            // Prefer the real image file name (e.g. "chrome.exe") taken from the executable
            // path, which always has the correct extension. Only fall back to the raw
            // Process.ProcessName (no fabricated extension) for pseudo-processes such as
            // "System", "Registry", and "Idle" that have no backing executable file.
            var name = !string.IsNullOrEmpty(executablePath)
                ? Path.GetFileName(executablePath)
                : process.ProcessName;

            try
            {
                if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                    publisher = string.IsNullOrWhiteSpace(versionInfo.CompanyName) ? "Unknown" : versionInfo.CompanyName!.Trim();
                }
            }
            catch (Exception ex) when (IsAccessError(ex) || ex is FileNotFoundException)
            {
                hasAccessDenied = true;
            }

            try
            {
                if (NativeMethods.GetProcessIoCounters(process.Handle, out ioCounters))
                {
                    hasIoCounters = true;
                }
            }
            catch (Exception ex) when (IsAccessError(ex))
            {
                hasAccessDenied = true;
            }

            var cpuUsagePercent = 0.0;
            var diskUsageMbPerSec = 0.0;

            var sample = new ProcessSample(totalProcessorTime, hasIoCounters ? ioCounters.ReadTransferCount + ioCounters.WriteTransferCount : 0);
            currentSamples[process.Id] = sample;

            if (_previousSamples.TryGetValue(process.Id, out var previous))
            {
                var cpuDelta = (totalProcessorTime - previous.TotalProcessorTime).TotalSeconds;
                cpuUsagePercent = Math.Max(0, cpuDelta / elapsedSeconds / processorCount * 100.0);

                if (hasIoCounters)
                {
                    var byteDelta = (long)sample.TotalIoBytes - (long)previous.TotalIoBytes;
                    if (byteDelta > 0)
                    {
                        diskUsageMbPerSec = byteDelta / 1024.0 / 1024.0 / elapsedSeconds;
                    }
                }
            }

            var isWindowsProcess = IsKnownWindowsPublisher(publisher) || IsSystemDirectory(executablePath);
            var startsWithWindows = !string.IsNullOrEmpty(executablePath) && startupPaths.Contains(executablePath);
            var safetyLevel = _classificationService.Classify(name, publisher, isWindowsProcess, startsWithWindows);

            var memoryMb = workingSetBytes / 1024.0 / 1024.0;

            return new ProcessInfo
            {
                ProcessId = process.Id,
                Name = name,
                CpuUsagePercent = Math.Round(cpuUsagePercent, 1),
                MemoryUsageMb = Math.Round(memoryMb, 1),
                MemoryUsagePercent = totalPhysicalMemoryMb > 0 ? Math.Round(memoryMb / totalPhysicalMemoryMb * 100.0, 1) : 0,
                DiskUsageMbPerSec = Math.Round(diskUsageMbPerSec, 2),
                // Per-process network throughput requires ETW tracing (Microsoft-Windows-TCPIP
                // provider) which is intentionally out of scope for the MVP; we report 0 rather
                // than a misleading estimate. See NetworkUsageKbPerSec doc comment.
                NetworkUsageKbPerSec = 0,
                Publisher = publisher,
                ExecutablePath = executablePath,
                IsWindowsProcess = isWindowsProcess || safetyLevel == ProcessSafetyLevel.CriticalWindowsProcess,
                StartsWithWindows = startsWithWindows,
                SafetyLevel = safetyLevel,
                HasAccessDeniedInfo = hasAccessDenied,
            };
        }
        catch (Exception ex) when (IsAccessError(ex) || ex is InvalidOperationException)
        {
            // Process exited between enumeration and inspection, or we don't have permission
            // to read anything about it at all. Skip it rather than crash the refresh cycle.
            return null;
        }
    }

    private static bool IsAccessError(Exception ex) =>
        ex is UnauthorizedAccessException or System.ComponentModel.Win32Exception;
    private static bool IsKnownWindowsPublisher(string publisher) =>
        publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return !string.IsNullOrEmpty(windowsDir) && path.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
    }

    private static double GetTotalPhysicalMemoryMb()
    {
        var (totalMb, _) = NativeMethods.GetPhysicalMemoryMb();
        return totalMb;
    }

    private readonly record struct ProcessSample(TimeSpan TotalProcessorTime, ulong TotalIoBytes);
}
