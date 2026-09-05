using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BootScope.Helpers;

/// <summary>
/// Minimal base class for MVVM view models. Implements <see cref="INotifyPropertyChanged"/>
/// and provides a helper to set backing fields and raise change notifications only when the
/// value actually changes, avoiding unnecessary UI refreshes.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
