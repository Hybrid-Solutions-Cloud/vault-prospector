using Avalonia.Controls;
using Avalonia.Input;
using VaultProspector.App.ViewModels;

namespace VaultProspector.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SearchBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel viewModel) await viewModel.SearchCommand.ExecuteAsync(null);
    }
}
