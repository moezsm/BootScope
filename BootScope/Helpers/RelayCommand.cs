using System.Windows.Input;

namespace BootScope.Helpers;

/// <summary>
/// Simple synchronous <see cref="ICommand"/> implementation used by view models.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Forces WPF to re-evaluate <see cref="CanExecute"/> for all commands bound in the UI.
    /// </summary>
    public static void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>
/// Command implementation that supports asynchronous execution while preventing
/// re-entrancy (the command disables itself while the task is running).
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isExecuting = true;
            RelayCommand.RaiseCanExecuteChanged();
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            // Execute is "async void" so exceptions can't be observed by callers; surface them
            // via the WPF dispatcher's unhandled exception mechanism instead of silently
            // swallowing them or crashing with no diagnostic information.
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred: {ex.Message}",
                    "BootScope",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error));
        }
        finally
        {
            _isExecuting = false;
            RelayCommand.RaiseCanExecuteChanged();
        }
    }
}
