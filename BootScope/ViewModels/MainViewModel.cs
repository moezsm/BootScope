using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using BootScope.Helpers;
using BootScope.Models;
using BootScope.Services;

namespace BootScope.ViewModels;

/// <summary>
/// Backs the main dashboard window: aggregate system usage, the live process table, and all
/// user-triggered process actions. All process/system polling runs off the UI thread; only the
/// resulting collection/property updates are applied on the dispatcher.
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly ProcessMonitorService _processMonitorService;
    private readonly SystemUsageService _systemUsageService;
    private readonly ProcessActionService _processActionService;
    private readonly SessionLogService _sessionLogService;
    private readonly SafetyClassificationService _classificationService;
    private readonly DispatcherTimer _refreshTimer;

    private AppSettings _settings;
    private bool _isRefreshing;
    private bool _hasLoggedStartupSession;
    private string _filterText = string.Empty;
    private ProcessInfo? _selectedProcess;
    private string _statusMessage = "Ready.";

    private double _cpuUsagePercent;
    private double _memoryUsagePercent;
    private double _memoryUsedMb;
    private double _memoryTotalMb;
    private double _diskUsagePercent;
    private double _networkUsageKbPerSec;

    public MainViewModel(
        ProcessMonitorService processMonitorService,
        SystemUsageService systemUsageService,
        ProcessActionService processActionService,
        SessionLogService sessionLogService,
        SafetyClassificationService classificationService,
        AppSettings settings)
    {
        _processMonitorService = processMonitorService;
        _systemUsageService = systemUsageService;
        _processActionService = processActionService;
        _sessionLogService = sessionLogService;
        _classificationService = classificationService;
        _settings = settings;

        Processes = new ObservableCollection<ProcessInfo>();
        ProcessesView = CollectionViewSource.GetDefaultView(Processes);
        ProcessesView.Filter = FilterProcess;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_isRefreshing);
        StopProcessCommand = new AsyncRelayCommand(StopSelectedProcessAsync, () => SelectedProcess is not null);
        OpenFileLocationCommand = new RelayCommand(() => OpenFileLocation(), () => SelectedProcess is not null);
        ShowPropertiesCommand = new RelayCommand(() => ShowProperties(), () => SelectedProcess is not null);
        SearchWebCommand = new RelayCommand(() => SearchWeb(), () => SelectedProcess is not null);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(_settings.RefreshIntervalSeconds, 1)),
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<ProcessInfo> Processes { get; }

    public ICollectionView ProcessesView { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand StopProcessCommand { get; }

    public RelayCommand OpenFileLocationCommand { get; }

    public RelayCommand ShowPropertiesCommand { get; }

    public RelayCommand SearchWebCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ProcessesView.Refresh();
            }
        }
    }

    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double CpuUsagePercent
    {
        get => _cpuUsagePercent;
        private set => SetProperty(ref _cpuUsagePercent, value);
    }

    public double MemoryUsagePercent
    {
        get => _memoryUsagePercent;
        private set => SetProperty(ref _memoryUsagePercent, value);
    }

    public double MemoryUsedMb
    {
        get => _memoryUsedMb;
        private set => SetProperty(ref _memoryUsedMb, value);
    }

    public double MemoryTotalMb
    {
        get => _memoryTotalMb;
        private set => SetProperty(ref _memoryTotalMb, value);
    }

    public double DiskUsagePercent
    {
        get => _diskUsagePercent;
        private set => SetProperty(ref _diskUsagePercent, value);
    }

    public double NetworkUsageKbPerSec
    {
        get => _networkUsageKbPerSec;
        private set => SetProperty(ref _networkUsageKbPerSec, value);
    }

    /// <summary>Applies updated settings (e.g. after the Settings page is closed) and restarts the timer.</summary>
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(_settings.RefreshIntervalSeconds, 1));
    }

    /// <summary>
    /// Starts the periodic dashboard/process refresh. Callers (App.xaml.cs) are responsible
    /// for delaying this call until the configured startup scan delay has elapsed, so
    /// BootScope does not itself add to boot-time load.
    /// </summary>
    public async Task StartAsync()
    {
        await RefreshAsync();
        _refreshTimer.Start();
    }

    private bool FilterProcess(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        if (obj is not ProcessInfo process)
        {
            return false;
        }

        return process.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
               || process.Publisher.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
               || process.ProcessId.ToString().Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        RelayCommand.RaiseCanExecuteChanged();
        try
        {
            var snapshotTask = _systemUsageService.GetSnapshotAsync();
            var processesTask = _processMonitorService.GetProcessSnapshotAsync(
                _settings.CpuWarningThresholdPercent, _settings.RamWarningThresholdPercent);

            await Task.WhenAll(snapshotTask, processesTask);

            ApplySystemSnapshot(snapshotTask.Result);
            MergeProcesses(processesTask.Result);

            if (!_hasLoggedStartupSession)
            {
                LogStartupSession();
                _hasLoggedStartupSession = true;
            }

            StatusMessage = $"Updated at {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private void ApplySystemSnapshot(SystemUsageSnapshot snapshot)
    {
        CpuUsagePercent = snapshot.CpuUsagePercent;
        MemoryUsagePercent = snapshot.MemoryUsagePercent;
        MemoryUsedMb = snapshot.MemoryUsedMb;
        MemoryTotalMb = snapshot.MemoryTotalMb;
        DiskUsagePercent = snapshot.DiskUsagePercent;
        NetworkUsageKbPerSec = snapshot.NetworkUsageKbPerSec;
    }

    /// <summary>
    /// Updates the existing <see cref="ProcessInfo"/> instances in place (by process id) so the
    /// DataGrid's sort/filter/selection state is preserved across refreshes, and adds/removes
    /// rows only for processes that started or exited since the last poll.
    /// </summary>
    private void MergeProcesses(IReadOnlyList<ProcessInfo> latest)
    {
        var latestById = latest.ToDictionary(p => p.ProcessId);
        var selectedId = SelectedProcess?.ProcessId;

        for (var i = Processes.Count - 1; i >= 0; i--)
        {
            if (!latestById.ContainsKey(Processes[i].ProcessId))
            {
                Processes.RemoveAt(i);
            }
        }

        var existingIds = Processes.ToDictionary(p => p.ProcessId);

        foreach (var updated in latest)
        {
            if (existingIds.TryGetValue(updated.ProcessId, out var existing))
            {
                existing.CpuUsagePercent = updated.CpuUsagePercent;
                existing.MemoryUsageMb = updated.MemoryUsageMb;
                existing.MemoryUsagePercent = updated.MemoryUsagePercent;
                existing.DiskUsageMbPerSec = updated.DiskUsageMbPerSec;
                existing.NetworkUsageKbPerSec = updated.NetworkUsageKbPerSec;
                existing.Publisher = updated.Publisher;
                existing.ExecutablePath = updated.ExecutablePath;
                existing.IsWindowsProcess = updated.IsWindowsProcess;
                existing.StartsWithWindows = updated.StartsWithWindows;
                existing.SafetyLevel = updated.SafetyLevel;
                existing.Recommendation = updated.Recommendation;
                existing.HasAccessDeniedInfo = updated.HasAccessDeniedInfo;
                existing.IsHighCpu = updated.IsHighCpu;
                existing.IsHighMemory = updated.IsHighMemory;
            }
            else
            {
                Processes.Add(updated);
            }
        }

        if (selectedId.HasValue)
        {
            SelectedProcess = Processes.FirstOrDefault(p => p.ProcessId == selectedId.Value);
        }
    }

    private void LogStartupSession()
    {
        var topCpu = Processes.OrderByDescending(p => p.CpuUsagePercent).Take(5).ToList();
        var topMemory = Processes.OrderByDescending(p => p.MemoryUsageMb).Take(5).ToList();
        _sessionLogService.RecordStartupSession(topCpu, topMemory);
    }

    private async Task StopSelectedProcessAsync()
    {
        var target = SelectedProcess;
        if (target is null)
        {
            return;
        }

        if (!ConfirmStop(target))
        {
            return;
        }

        StatusMessage = $"Stopping {target.Name} (PID {target.ProcessId})...";
        var result = await _processActionService.StopProcessAsync(target.ProcessId);

        StatusMessage = result.IsSuccess
            ? $"Stopped {target.Name} (PID {target.ProcessId})."
            : result.ErrorMessage ?? "Failed to stop process.";

        await RefreshAsync();
    }

    /// <summary>
    /// Shows a confirmation dialog before terminating any process. Critical Windows processes
    /// get a strong, explicit warning; the user must still actively choose "Yes" to proceed.
    /// This is the single safety gate that stands between the UI and process termination.
    /// </summary>
    private bool ConfirmStop(ProcessInfo process)
    {
        string message;
        string caption;
        var icon = MessageBoxImage.Warning;

        if (process.SafetyLevel == ProcessSafetyLevel.CriticalWindowsProcess)
        {
            caption = "⚠ Critical Windows Process";
            icon = MessageBoxImage.Error;
            message = $"\"{process.Name}\" (PID {process.ProcessId}) is a CRITICAL Windows system process.\n\n" +
                      "Stopping it is very likely to CRASH or DESTABILIZE Windows, and may require you to " +
                      "restart your computer or could cause data loss.\n\n" +
                      "BootScope strongly recommends you do NOT stop this process.\n\n" +
                      "Are you absolutely sure you want to continue anyway?";
        }
        else if (process.SafetyLevel == ProcessSafetyLevel.WindowsBackgroundProcess)
        {
            caption = "Stop Windows Process";
            message = $"\"{process.Name}\" (PID {process.ProcessId}) is a Windows background process.\n\n" +
                      "Closing it is usually safe, but some Windows features may stop working until it restarts.\n\n" +
                      "Do you want to stop this process?";
        }
        else
        {
            caption = "Stop Process";
            message = $"Are you sure you want to stop \"{process.Name}\" (PID {process.ProcessId})?\n\n" +
                      "Any unsaved work in this application will be lost.";
        }

        var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, icon, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }

    private void OpenFileLocation()
    {
        if (SelectedProcess is not null)
        {
            _processActionService.OpenFileLocation(SelectedProcess.ExecutablePath);
        }
    }

    private void ShowProperties()
    {
        if (SelectedProcess is not null)
        {
            _processActionService.ShowFileProperties(SelectedProcess.ExecutablePath);
        }
    }

    private void SearchWeb()
    {
        if (SelectedProcess is not null)
        {
            _processActionService.SearchProcessNameOnWeb(SelectedProcess.Name);
        }
    }
}
