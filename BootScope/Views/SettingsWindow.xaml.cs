using System.Windows;
using BootScope.ViewModels;

namespace BootScope.Views;

/// <summary>
/// Settings window code-behind. Simply wires the DataContext and closes the window when the
/// user saves or cancels; all persistence logic lives in <see cref="SettingsViewModel"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ((SettingsViewModel)DataContext).SaveCommand.Execute(null);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
