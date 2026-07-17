using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VaultProspector.App.ViewModels;
using VaultProspector.App.Views;
using VaultProspector.Application;
using VaultProspector.Infrastructure;
using VaultProspector.Platform;
using VaultProspector.Providers.Azure;

namespace VaultProspector.App;

public partial class App : Avalonia.Application
{
    private static readonly double[] FontSizes = [11, 12, 14, 16, 17, 18, 20, 22, 24];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ApplyWindowsTextScale();
            var keyProvider = new WindowsDataProtectionKeyProvider(VaultProspectorPaths.KeyDirectory);
            var clock = new SystemClock();
            var repository = new EncryptedSqliteMetadataRepository(VaultProspectorPaths.DatabasePath, keyProvider);
            var identityProvider = new MsalIdentityProvider(VaultProspectorPaths.TokenCacheDirectory);
            var azureProvider = new AzureVaultProvider(identityProvider);
            var diagnostics = new RedactingDiagnosticSink(VaultProspectorPaths.LogPath);
            var valueStore = new EncryptedFileValueStore(VaultProspectorPaths.CacheDirectory, keyProvider, clock);
            var clipboard = new AvaloniaClipboardService();
            IUserVerificationService verification = new WindowsHelloVerificationService();
            var viewModel = new MainViewModel(
                repository,
                new IdentityService(identityProvider, repository),
                new SynchronizationService(azureProvider, repository, clock, diagnostics),
                new SearchService(repository, clock),
                new SecretAccessService(azureProvider, repository, valueStore, clipboard, verification, clock),
                new WorkspaceService(repository),
                valueStore,
                new AppSettingsStore(Path.Combine(VaultProspectorPaths.DataDirectory, "settings.json")));
            var window = new MainWindow { DataContext = viewModel };
            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            var clipboardCleanupInProgress = false;
            var clipboardCleanupCompleted = false;
            window.Closing += async (_, args) =>
            {
                if (clipboardCleanupCompleted) return;
                args.Cancel = true;
                if (clipboardCleanupInProgress) return;
                clipboardCleanupInProgress = true;
                try
                {
                    await clipboard.ClearIfOwnedAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    diagnostics.WriteError("clipboard_shutdown_clear_failed", exception, new Dictionary<string, object?>());
                }
                finally
                {
                    clipboardCleanupCompleted = true;
                    clipboardCleanupInProgress = false;
                    window.Close();
                }
            };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyWindowsTextScale()
    {
        var factor = WindowsTextScale.ReadFactor();
        foreach (var baseSize in FontSizes)
            Resources[$"VaultFontSize{baseSize:0}"] = baseSize * factor;
    }
}
