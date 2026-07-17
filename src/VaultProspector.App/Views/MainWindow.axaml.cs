using Avalonia.Controls;
using Avalonia.Input;
using VaultProspector.App.ViewModels;

namespace VaultProspector.App.Views;

public partial class MainWindow : Window
{
    private const double NarrowLayoutThreshold = 720;
    private bool? _isNarrowLayout;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => FitToWorkingArea();
        SizeChanged += (_, _) => ApplyResponsiveLayout(ClientSize.Width < NarrowLayoutThreshold);
    }

    private async void SearchBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel viewModel) await viewModel.SearchCommand.ExecuteAsync(null);
    }

    private void FitToWorkingArea()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;

        var metrics = WindowLayoutMetrics.Fit(
            Width,
            Height,
            screen.WorkingArea.Width,
            screen.WorkingArea.Height,
            screen.Scaling,
            NarrowLayoutThreshold);
        Width = metrics.Width;
        Height = metrics.Height;
        ApplyResponsiveLayout(metrics.UseNarrowLayout);
    }

    private void ApplyResponsiveLayout(bool narrow)
    {
        if (_isNarrowLayout == narrow) return;
        _isNarrowLayout = narrow;

        HeaderContextBadge.IsVisible = !narrow;

        SearchCommandGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,150,Auto");
        SearchCommandGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto,Auto" : "Auto");
        Place(ObjectTypeComboBox, narrow ? 1 : 0, narrow ? 0 : 1);
        Place(SearchButton, narrow ? 2 : 0, narrow ? 0 : 2);

        SearchFilterGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,*,*");
        SearchFilterGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto,Auto" : "Auto");
        Place(SubscriptionFilterTextBox, narrow ? 1 : 0, narrow ? 0 : 1);
        Place(VaultFilterTextBox, narrow ? 2 : 0, narrow ? 0 : 2);

        SearchResultsGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "2*,1*");
        SearchResultsGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "*");
        Place(SelectedObjectCard, narrow ? 1 : 0, narrow ? 0 : 1);

        IdentityGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "1*,1*");
        IdentityGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "*");
        Place(ConnectIdentityCard, narrow ? 1 : 0, narrow ? 0 : 1);

        WorkspaceGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "1*,1*");
        WorkspaceGrid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "*");
        Place(CreateWorkspaceCard, narrow ? 1 : 0, narrow ? 0 : 1);
    }

    private static void Place(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
    }
}
