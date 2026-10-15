using Avalonia.Controls;
using SystemMonitor.ViewModels;

namespace MonWin.Linux.Views;

/// <summary>
/// A normal window (title bar, movable, closable by Save/Cancel). Unlike the Windows flyout it
/// does not dismiss on outside clicks: that relies on a global mouse hook, which Linux desktops
/// don't allow ordinary apps to install.
/// </summary>
public partial class SettingsWindow : Window
{
    // Required by the XAML loader/previewer.
    public SettingsWindow()
    {
        InitializeComponent();
    }

    internal SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
