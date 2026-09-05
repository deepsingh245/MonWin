using System.Windows;
using SystemMonitor.ViewModels;

namespace SystemMonitor.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
