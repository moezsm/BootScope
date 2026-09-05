using System.Windows;
using BootScope.ViewModels;

namespace BootScope.Views;

/// <summary>
/// Code-behind for the main dashboard window. Kept intentionally thin: all logic lives in
/// <see cref="MainViewModel"/>; this class only wires up the DataContext and opens the
/// Settings window on request.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ShowSettingsWindow(this);
        }
    }
}
