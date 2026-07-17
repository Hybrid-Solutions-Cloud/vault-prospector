using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VaultProspector.App.ViewModels;

namespace VaultProspector.App.Views;

public partial class MainWindow : Window
{
    private const double NarrowLayoutThreshold = 720;
    private readonly double _textScaleFactor;
    private bool? _isNarrowLayout;
    private IPlatformSettings? _platformSettings;
    private MainViewModel? _subscribedViewModel;
    private readonly FocusReturnCoordinator<Control> _focusReturn = new();

    public MainWindow()
    {
        InitializeComponent();
        _textScaleFactor = WindowsTextScale.ReadFactor();
        AddHandler(InputElement.GotFocusEvent, MainWindow_OnGotFocus, RoutingStrategies.Bubble, true);
        DataContextChanged += MainWindow_OnDataContextChanged;
        Activated += MainWindow_OnActivated;
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
        SizeChanged += (_, _) => ApplyResponsiveLayout(
            WindowLayoutMetrics.RequiresNarrow(ClientSize.Width, _textScaleFactor, NarrowLayoutThreshold));
    }

    private async void SearchBox_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel viewModel) await viewModel.SearchCommand.ExecuteAsync(null);
    }

    private void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        FitToWorkingArea();
        _platformSettings = this.GetPlatformSettings();
        if (_platformSettings is null) return;
        ApplyContrastPreference(_platformSettings.GetColorValues());
        _platformSettings.ColorValuesChanged += PlatformSettings_OnColorValuesChanged;
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        DataContextChanged -= MainWindow_OnDataContextChanged;
        Activated -= MainWindow_OnActivated;
        UnsubscribeFromViewModel();
        if (_platformSettings is not null)
            _platformSettings.ColorValuesChanged -= PlatformSettings_OnColorValuesChanged;
        _platformSettings = null;
    }

    private void MainWindow_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is Control control) _focusReturn.Remember(control);
    }

    private void MainWindow_OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeFromViewModel();
        if (DataContext is not MainViewModel viewModel) return;
        _subscribedViewModel = viewModel;
        _subscribedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _subscribedViewModel = null;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsBusy) || sender is not MainViewModel viewModel) return;
        if (viewModel.IsBusy)
        {
            var focused = FocusManager?.GetFocusedElement() as Control;
            _focusReturn.OperationStarted(focused);
            return;
        }

        _focusReturn.OperationCompleted();
        Dispatcher.UIThread.Post(TryRestoreOperationFocus, DispatcherPriority.Input);
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        if (_focusReturn.IsRestorePending)
            Dispatcher.UIThread.Post(TryRestoreOperationFocus, DispatcherPriority.Input);
    }

    private void TryRestoreOperationFocus()
    {
        var control = _focusReturn.TakeRestoreTarget(IsActive, static candidate =>
            candidate.Focusable &&
            candidate.IsEffectivelyEnabled &&
            candidate.IsEffectivelyVisible &&
            candidate.IsAttachedToVisualTree());
        if (control is null) return;
        control.Focus(NavigationMethod.Tab);
    }

    private void PlatformSettings_OnColorValuesChanged(object? sender, PlatformColorValues values) =>
        ApplyContrastPreference(values);

    private void ApplyContrastPreference(PlatformColorValues values)
    {
        const string highContrastClass = "high-contrast";
        if (values.ContrastPreference == ColorContrastPreference.High)
        {
            if (!Classes.Contains(highContrastClass)) Classes.Add(highContrastClass);
        }
        else
        {
            Classes.Remove(highContrastClass);
        }
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
            _textScaleFactor,
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
