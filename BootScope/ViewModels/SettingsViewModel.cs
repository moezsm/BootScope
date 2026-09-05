using BootScope.Helpers;
using BootScope.Models;
using BootScope.Services;

namespace BootScope.ViewModels;

/// <summary>
/// Backs the Settings window: launch-at-login toggle, startup scan delay, refresh interval,
/// and the CPU/RAM warning thresholds used to highlight processes in the dashboard.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly StartupRegistrationService _startupRegistrationService;

    private bool _launchAtLogin;
    private int _startupScanDelaySeconds;
    private int _refreshIntervalSeconds;
    private double _cpuWarningThresholdPercent;
    private double _ramWarningThresholdPercent;

    public SettingsViewModel(SettingsService settingsService, StartupRegistrationService startupRegistrationService, AppSettings settings)
    {
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;

        _launchAtLogin = settings.LaunchAtLogin;
        _startupScanDelaySeconds = settings.StartupScanDelaySeconds;
        _refreshIntervalSeconds = settings.RefreshIntervalSeconds;
        _cpuWarningThresholdPercent = settings.CpuWarningThresholdPercent;
        _ramWarningThresholdPercent = settings.RamWarningThresholdPercent;

        SaveCommand = new RelayCommand(_ => Save());
    }

    /// <summary>Raised after settings are saved so the main window can apply them immediately.</summary>
    public event EventHandler<AppSettings>? SettingsSaved;

    public RelayCommand SaveCommand { get; }

    /// <summary>
    /// Enabling this only writes a per-user registry Run key (HKCU); it never requires
    /// administrator privileges and never touches Windows services.
    /// </summary>
    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set => SetProperty(ref _launchAtLogin, value);
    }

    public int StartupScanDelaySeconds
    {
        get => _startupScanDelaySeconds;
        set => SetProperty(ref _startupScanDelaySeconds, Math.Max(0, value));
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set => SetProperty(ref _refreshIntervalSeconds, Math.Max(1, value));
    }

    public double CpuWarningThresholdPercent
    {
        get => _cpuWarningThresholdPercent;
        set => SetProperty(ref _cpuWarningThresholdPercent, Math.Clamp(value, 1, 100));
    }

    public double RamWarningThresholdPercent
    {
        get => _ramWarningThresholdPercent;
        set => SetProperty(ref _ramWarningThresholdPercent, Math.Clamp(value, 1, 100));
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            LaunchAtLogin = LaunchAtLogin,
            StartupScanDelaySeconds = StartupScanDelaySeconds,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            CpuWarningThresholdPercent = CpuWarningThresholdPercent,
            RamWarningThresholdPercent = RamWarningThresholdPercent,
        };

        _settingsService.Save(settings);
        // Launch-at-login only ever modifies the current user's own Run key - no admin
        // elevation is requested or required for this action.
        _startupRegistrationService.SetLaunchAtLogin(settings.LaunchAtLogin);

        SettingsSaved?.Invoke(this, settings);
    }
}
