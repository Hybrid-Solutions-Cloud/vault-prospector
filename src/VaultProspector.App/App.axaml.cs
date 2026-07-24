using System.ComponentModel;
using System.Net.NetworkInformation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using VaultProspector.App.ViewModels;
using VaultProspector.App.Views;
using VaultProspector.Application;
using VaultProspector.Domain;
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
            var localDataRecovery = new LocalDataRecoveryService(
                verification,
                new FileSystemLocalDataResetter(VaultProspectorPaths.DataDirectory));
            var localRecoveryArchiveService =
                new LocalRecoveryArchiveService(
                    verification,
                    new FileSystemLocalRecoveryArchiveStore(
                        VaultProspectorPaths.DataDirectory),
                    diagnostics);
            var managedIdentityHttpClient = new HttpClient(
                new SocketsHttpHandler { UseProxy = false })
            {
                Timeout = TimeSpan.FromSeconds(2),
            };
            var graphHttpClient = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            var authorizationHttpClient = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            })
            {
                Timeout = TimeSpan.FromSeconds(30),
            };
            var viewModel = new MainViewModel(
                repository,
                new IdentityService(
                    identityProvider,
                    repository,
                    diagnostics,
                    valueStore),
                new SynchronizationService(azureProvider, repository, clock, diagnostics),
                new SearchService(repository, clock),
                new SecretAccessService(azureProvider, repository, valueStore, clipboard, verification, clock),
                new WorkspaceService(repository),
                valueStore,
                new AppSettingsStore(Path.Combine(VaultProspectorPaths.DataDirectory, "settings.json")),
                verification,
                localDataRecovery,
                new ManagedIdentityEnvironmentDetector(managedIdentityHttpClient),
                new WorkloadIdentityDiscoveryService(
                    identityProvider,
                    graphHttpClient,
                    authorizationHttpClient),
                new LocalEncryptionRotationEngine(
                    VaultProspectorPaths.DataDirectory,
                    VaultProspectorPaths.DatabasePath,
                    VaultProspectorPaths.CacheDirectory,
                    keyProvider,
                    clock),
                localRecoveryArchiveService);
            var window = new MainWindow { DataContext = viewModel };
            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var showItem = new NativeMenuItem("Show Vault Prospector");
            var statusItem = new NativeMenuItem("Starting securely…") { IsEnabled = false };
            var exitItem = new NativeMenuItem("Exit");
            var trayMenu = new NativeMenu();
            trayMenu.Add(showItem);
            trayMenu.Add(statusItem);
            trayMenu.Add(new NativeMenuItemSeparator());
            trayMenu.Add(exitItem);
            var trayIcon = new TrayIcon
            {
                Icon = window.Icon,
                ToolTipText = "Vault Prospector — starting securely",
                Menu = trayMenu,
                IsVisible = true,
            };
            TrayIcon.SetIcons(this, [trayIcon]);

            var clipboardCleanupInProgress = false;
            var clipboardCleanupCompleted = false;
            var allowShutdown = false;
            var trayDisposed = false;
            WindowsSecurityBoundaryMonitor? securityBoundaryMonitor = null;
            void DisposeTray()
            {
                if (trayDisposed) return;
                trayDisposed = true;
                trayIcon.IsVisible = false;
                trayIcon.Dispose();
            }

            async Task ExitAsync()
            {
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
                    allowShutdown = true;
                    DisposeTray();
                    desktop.Shutdown();
                }
            }

            void ShowWindow()
            {
                window.ShowInTaskbar = true;
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
            }

            void ContinueInBackground()
            {
                viewModel.LockForBackground();
                window.ShowInTaskbar = false;
                window.Hide();
            }

            void RefreshTrayState(object? sender = null, PropertyChangedEventArgs? args = null)
            {
                var state = TrayStatusPolicy.Describe(
                    NetworkInterface.GetIsNetworkAvailable(),
                    viewModel.IsBusy,
                    viewModel.HasActionableError,
                    viewModel.IsUnlocked,
                    viewModel.SelectedIdentity?.AuthenticationState ==
                    AuthenticationState.InteractionRequired);
                statusItem.Header = state;
                trayIcon.ToolTipText = $"Vault Prospector — {state}";
            }

            showItem.Click += (_, _) => ShowWindow();
            exitItem.Click += async (_, _) => await ExitAsync();
            trayIcon.Clicked += (_, _) => ShowWindow();
            viewModel.ExitRequested += async (_, _) => await ExitAsync();
            viewModel.ContinueInBackgroundRequested += (_, _) => ContinueInBackground();
            viewModel.PropertyChanged += RefreshTrayState;
            NetworkAvailabilityChangedEventHandler networkAvailabilityChanged = (_, _) =>
                Dispatcher.UIThread.Post(() => RefreshTrayState());
            NetworkChange.NetworkAvailabilityChanged += networkAvailabilityChanged;
            void LockForWindowsSecurityBoundary(
                object? sender,
                WindowsSecurityBoundaryEventArgs args) =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (allowShutdown)
                        return;

                    viewModel.LockForSystemBoundary();
                    RefreshTrayState();
                });
            try
            {
                securityBoundaryMonitor = new WindowsSecurityBoundaryMonitor();
                securityBoundaryMonitor.LockRequired += LockForWindowsSecurityBoundary;
            }
            catch (Exception exception)
            {
                diagnostics.WriteError(
                    "windows_security_boundary_monitor_unavailable",
                    exception,
                    new Dictionary<string, object?>());
            }
            window.Closing += (_, args) =>
            {
                if (allowShutdown) return;
                args.Cancel = true;
                switch (viewModel.SelectedCloseBehavior)
                {
                    case CloseBehavior.Exit:
                        _ = ExitAsync();
                        break;
                    case CloseBehavior.LockToNotificationArea:
                        ContinueInBackground();
                        break;
                    default:
                        viewModel.RequestCloseChoice();
                        break;
                }
            };

            var backgroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            backgroundTimer.Tick += async (_, _) =>
            {
                var powerKnown = BackgroundSyncPolicy.TryIsOnExternalPower(out var onExternalPower);
                if (BackgroundSyncPolicy.IsEligible(
                        viewModel.BackgroundMetadataSyncEnabled,
                        !window.IsVisible,
                        NetworkInterface.GetIsNetworkAvailable(),
                        powerKnown,
                        onExternalPower))
                    await viewModel.BackgroundSynchronizeOnceAsync();
                RefreshTrayState();
            };
            backgroundTimer.Start();
            desktop.Exit += (_, _) =>
            {
                backgroundTimer.Stop();
                viewModel.PropertyChanged -= RefreshTrayState;
                NetworkChange.NetworkAvailabilityChanged -= networkAvailabilityChanged;
                if (securityBoundaryMonitor is not null)
                {
                    securityBoundaryMonitor.LockRequired -= LockForWindowsSecurityBoundary;
                    securityBoundaryMonitor.Dispose();
                }
                if (!clipboardCleanupCompleted)
                {
                    try
                    {
                        clipboard.ClearIfOwnedAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (Exception exception)
                    {
                        diagnostics.WriteError("clipboard_shutdown_clear_failed", exception, new Dictionary<string, object?>());
                    }
                }
                DisposeTray();
            };
            desktop.MainWindow = window;
            RefreshTrayState();
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
