using System.Diagnostics;
using BootScope.Helpers;
using BootScope.Models;

namespace BootScope.Services;

/// <summary>
/// Computes aggregate system-wide resource usage for the dashboard header (total CPU, RAM,
/// disk, and network usage) using standard Windows performance counters.
/// </summary>
public class SystemUsageService : IDisposable
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private PerformanceCounter[]? _networkCounters;
    private bool _initialized;

    public Task<SystemUsageSnapshot> GetSnapshotAsync()
    {
        return Task.Run(() =>
        {
            EnsureInitialized();

            var cpu = SafeNextValue(_cpuCounter);
            var disk = SafeNextValue(_diskCounter);
            double network = 0;
            if (_networkCounters is not null)
            {
                foreach (var counter in _networkCounters)
                {
                    network += SafeNextValue(counter);
                }
            }

            var (usedMb, totalMb) = GetMemoryUsage();

            return new SystemUsageSnapshot
            {
                CpuUsagePercent = Math.Round(cpu, 1),
                MemoryUsedMb = Math.Round(usedMb, 1),
                MemoryTotalMb = Math.Round(totalMb, 1),
                DiskUsagePercent = Math.Round(disk, 1),
                // "Bytes Total/sec" counter is reported in bytes/sec; convert to KB/sec for display.
                NetworkUsageKbPerSec = Math.Round(network / 1024.0, 1),
            };
        });
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");

            var category = new PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames();
            _networkCounters = instances
                .Select(instance => TryCreateCounter("Network Interface", "Bytes Total/sec", instance))
                .Where(counter => counter is not null)
                .Select(counter => counter!)
                .ToArray();

            // Performance counters require a warm-up sample before the first reading is
            // meaningful; discard the initial (always-zero) sample here.
            _cpuCounter.NextValue();
            _diskCounter.NextValue();
            foreach (var counter in _networkCounters)
            {
                counter.NextValue();
            }
        }
        catch (Exception)
        {
            // Performance counters can be unavailable (e.g. disabled service, restricted
            // environment). Fall back gracefully to zeroed readings rather than crashing.
        }
        finally
        {
            _initialized = true;
        }
    }

    private static PerformanceCounter? TryCreateCounter(string category, string counterName, string instance)
    {
        try
        {
            return new PerformanceCounter(category, counterName, instance);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double SafeNextValue(PerformanceCounter? counter)
    {
        try
        {
            return counter?.NextValue() ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static (double usedMb, double totalMb) GetMemoryUsage()
    {
        var (totalMb, availMb) = NativeMethods.GetPhysicalMemoryMb();
        return (totalMb - availMb, totalMb);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
        if (_networkCounters is not null)
        {
            foreach (var counter in _networkCounters)
            {
                counter.Dispose();
            }
        }
    }
}
