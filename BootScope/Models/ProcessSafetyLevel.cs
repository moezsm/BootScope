namespace BootScope.Models;

/// <summary>
/// Safety classification for a running process. This drives the UI badge colour and the
/// recommendation shown to the user. BootScope never acts on this classification by itself -
/// it is purely informational and always requires explicit user action to close a process.
/// </summary>
public enum ProcessSafetyLevel
{
    /// <summary>
    /// Core Windows process required for the operating system to function
    /// (e.g. System, Registry, smss.exe, csrss.exe, wininit.exe, services.exe, lsass.exe,
    /// svchost.exe, dwm.exe, explorer.exe). Closing these can crash or destabilize Windows.
    /// </summary>
    CriticalWindowsProcess,

    /// <summary>
    /// Signed Microsoft/Windows process that supports OS features but is not fatal to close
    /// (e.g. background Windows services, Search Indexer, Windows Defender helpers).
    /// </summary>
    WindowsBackgroundProcess,

    /// <summary>
    /// A recognised non-Microsoft application (e.g. a browser, IDE, or other installed
    /// software) identified by publisher metadata or a known executable path.
    /// </summary>
    ThirdPartyApplication,

    /// <summary>
    /// A process that was launched automatically at Windows startup (Run keys, Startup
    /// folder, or Scheduled Tasks) and is therefore a likely contributor to boot-time load.
    /// </summary>
    StartupApplication,

    /// <summary>
    /// A process BootScope could not confidently classify, e.g. because its publisher
    /// information or path could not be read (often due to access-denied errors).
    /// </summary>
    Unknown
}

/// <summary>
/// Human-facing recommendation shown next to a process. This is only guidance - BootScope
/// never terminates a process automatically and always requires explicit confirmation.
/// </summary>
public enum ProcessRecommendation
{
    /// <summary>Generally safe for the user to close if they don't need it right now.</summary>
    SafeToClose,

    /// <summary>Only close this if the user isn't actively using the related application.</summary>
    CloseOnlyIfUnused,

    /// <summary>Never suggest closing this process; it is required for Windows to run.</summary>
    DoNotClose,

    /// <summary>BootScope could not determine a safe recommendation; the user should research it.</summary>
    NeedsInvestigation
}
