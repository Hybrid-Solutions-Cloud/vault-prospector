using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VaultProspector.Mobile.ViewModels;
using VaultProspector.Mobile.Views;

namespace VaultProspector.Mobile;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not ISingleViewApplicationLifetime singleView)
        {
            throw new PlatformNotSupportedException(
                "Vault Prospector mobile requires a single-view application lifetime.");
        }

        var services = MobileRuntime.GetRequiredServices();
        var viewModel = new MainViewModel(services);
        MobileRuntime.Attach(viewModel);
        singleView.MainView = new MainView { DataContext = viewModel };
        _ = viewModel.InitializeAsync();
        base.OnFrameworkInitializationCompleted();
    }
}
