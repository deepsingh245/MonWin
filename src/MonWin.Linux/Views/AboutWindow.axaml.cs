using Avalonia.Controls;
using Avalonia.Interactivity;
using SystemMonitor.ViewModels;

namespace MonWin.Linux.Views;

public partial class AboutWindow : Window
{
    // Required by the XAML loader/previewer.
    public AboutWindow()
    {
        InitializeComponent();
    }

    internal AboutWindow(AboutViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
