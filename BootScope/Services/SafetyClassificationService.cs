using BootScope.Models;

namespace BootScope.Services;

/// <summary>
/// Determines the safety classification and recommendation for a process. This is the single
/// place that decides whether a process is "critical" - keeping this logic centralized makes
/// it easy to audit and extend the safety rules.
///
/// SAFETY NOTE: this service never terminates or disables anything. It only produces labels
/// that the UI uses to warn the user. All destructive actions still require explicit
/// confirmation from a human (see <see cref="ProcessActionService"/>).
/// </summary>
public class SafetyClassificationService
{
    /// <summary>
    /// Process image names (without path) that are essential to Windows booting and running.
    /// Closing any of these can crash or destabilize the operating system, so BootScope must
    /// never suggest closing them and must show a strong warning if the user tries anyway.
    /// </summary>
    private static readonly HashSet<string> CriticalWindowsProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "System Idle Process",
        "Registry",
        "smss.exe",
        "csrss.exe",
        "wininit.exe",
        "winlogon.exe",
        "services.exe",
        "lsass.exe",
        "svchost.exe",
        "dwm.exe",
        "explorer.exe",
        "fontdrvhost.exe",
        "sihost.exe",
        "lsm.exe",
    };

    /// <summary>
    /// Non-critical but well-known Windows background processes. These are safe-ish to close
    /// in most cases but are still part of Windows, so BootScope recommends caution rather
    /// than an outright "safe to close".
    /// </summary>
    private static readonly HashSet<string> WindowsBackgroundProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SearchIndexer.exe",
        "SearchApp.exe",
        "RuntimeBroker.exe",
        "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe",
        "TextInputHost.exe",
        "SecurityHealthService.exe",
        "SecurityHealthSystray.exe",
        "MsMpEng.exe",
        "WmiPrvSE.exe",
        "spoolsv.exe",
        "audiodg.exe",
        "ctfmon.exe",
        "taskhostw.exe",
        "dllhost.exe",
    };

    /// <summary>
    /// Classifies a process based on its name, whether it's Microsoft-signed/published, and
    /// whether it is registered to start with Windows.
    /// </summary>
    public ProcessSafetyLevel Classify(string processName, string publisher, bool isWindowsProcess, bool startsWithWindows)
    {
        if (CriticalWindowsProcessNames.Contains(processName))
        {
            return ProcessSafetyLevel.CriticalWindowsProcess;
        }

        if (isWindowsProcess || WindowsBackgroundProcessNames.Contains(processName) || IsMicrosoftPublisher(publisher))
        {
            return ProcessSafetyLevel.WindowsBackgroundProcess;
        }

        if (startsWithWindows)
        {
            return ProcessSafetyLevel.StartupApplication;
        }

        if (!string.IsNullOrWhiteSpace(publisher) && !string.Equals(publisher, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessSafetyLevel.ThirdPartyApplication;
        }

        return ProcessSafetyLevel.Unknown;
    }

    /// <summary>
    /// Maps a safety level (plus current resource usage) to a human-facing recommendation.
    /// High CPU/RAM usage never escalates a critical process to "safe to close" - safety
    /// classification always wins over usage-based heuristics.
    /// </summary>
    public ProcessRecommendation Recommend(ProcessSafetyLevel safetyLevel, double cpuUsagePercent, double memoryUsagePercent, double cpuThreshold, double ramThreshold)
    {
        switch (safetyLevel)
        {
            case ProcessSafetyLevel.CriticalWindowsProcess:
                return ProcessRecommendation.DoNotClose;

            case ProcessSafetyLevel.WindowsBackgroundProcess:
                return ProcessRecommendation.CloseOnlyIfUnused;

            case ProcessSafetyLevel.StartupApplication:
                return cpuUsagePercent >= cpuThreshold || memoryUsagePercent >= ramThreshold
                    ? ProcessRecommendation.CloseOnlyIfUnused
                    : ProcessRecommendation.SafeToClose;

            case ProcessSafetyLevel.ThirdPartyApplication:
                return ProcessRecommendation.SafeToClose;

            case ProcessSafetyLevel.Unknown:
            default:
                return ProcessRecommendation.NeedsInvestigation;
        }
    }

    public bool IsCritical(string processName) => CriticalWindowsProcessNames.Contains(processName);

    private static bool IsMicrosoftPublisher(string publisher) =>
        publisher is not null && publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
}
