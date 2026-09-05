using System.Configuration;
using System.Data;
using System.Windows;
using BootScope.Models;
using BootScope.Services;
using BootScope.ViewModels;
using BootScope.Views;

namespace BootScope;

/// <summary>
/// Application entry point and lightweight composition root. BootScope intentionally uses
/// plain constructor wiring instead of a DI container to keep the dependency graph small and
/// easy to audit for a safety-sensitive tool like this.
///
/// STARTUP SAFETY: the main window is shown immediately (so the user always has visibility
/// into BootScope and can open Settings), but the actual process/resource scan is delayed by
/// <see cref="AppSettings.StartupScanDelaySeconds"/> (45s by default) so that BootScope itself
/// does not add to the load right after the user logs in.
/// </summary>
public partial class App : Application
{
    private SettingsService _settingsService = null!;
    private AppSettings _settings = null!;
    private MainViewModel _mainViewModel = null!;
    private ProcessActionService _processActionService = null!;
    private SessionLogService _sessionLogService = null!;
    private SafetyClassificationService _classificationService = null!;
    private StartupDetectionService _startupDetectionService = null!;
    private ProcessMonitorService _processMonitorService = null!;
    private SystemUsageService _systemUsageService = null!;
    private StartupRegistrationService _startupRegistrationService = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // BootScope always starts with standard user permissions. It never requests
        // administrator elevation at launch; elevation (if ever needed for a specific
        // administrative action) would only be requested at the point the user chooses that
        // action, and is out of scope for the read-only MVP features implemented here.
        _settingsService = new SettingsService();
        var isFirstLaunch = !_settingsService.SettingsFileExists();
        _settings = _settingsService.Load();

        _classificationService = new SafetyClassificationService();
        _startupDetectionService = new StartupDetectionService();
        _processMonitorService = new ProcessMonitorService(_classificationService, _startupDetectionService);
        _systemUsageService = new SystemUsageService();
        _processActionService = new ProcessActionService();
        _sessionLogService = new SessionLogService();
        _startupRegistrationService = new StartupRegistrationService();

        if (isFirstLaunch)
        {
            // No settings file yet: this is the very first run after a fresh install (or a
            // portable copy run for the first time). Reflect whatever the installer's
            // "Launch BootScope when I sign in to Windows" task actually configured (if
            // anything) instead of overwriting it with the in-app default, and without ever
            // writing to the registry ourselves here - this keeps the installer option and the
            // in-app Settings toggle in sync and avoids creating a duplicate/conflicting
            // startup entry on first launch.
            _settings.LaunchAtLogin = _startupRegistrationService.IsRegistered();
            _settingsService.Save(_settings);
        }

        _mainViewModel = new MainViewModel(
            _processMonitorService,
            _systemUsageService,
            _processActionService,
            _sessionLogService,
            _classificationService,
            _settings);

        var mainWindow = new MainWindow(_mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();

        var delaySeconds = Math.Max(_settings.StartupScanDelaySeconds, 0);
        if (delaySeconds > 0)
        {
            _mainViewModel.StatusMessage = $"Startup scan begins in {delaySeconds}s to avoid adding to boot load...";
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        await _mainViewModel.StartAsync();
    }

    /// <summary>Opens the modal Settings window and applies any saved changes immediately.</summary>
    public void ShowSettingsWindow(Window owner)
    {
        var settingsViewModel = new SettingsViewModel(_settingsService, _startupRegistrationService, _settings);
        settingsViewModel.SettingsSaved += (_, updatedSettings) =>
        {
            _settings = updatedSettings;
            _mainViewModel.ApplySettings(updatedSettings);
        };

        var settingsWindow = new SettingsWindow(settingsViewModel) { Owner = owner };
        settingsWindow.ShowDialog();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _systemUsageService?.Dispose();
        base.OnExit(e);
    }
}

