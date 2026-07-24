using VaultProspector.Mobile.Core;
using VaultProspector.Mobile.ViewModels;

namespace VaultProspector.Mobile;

public static class MobileRuntime
{
    private static readonly object Gate = new();
    private static IMobilePlatformServices? _services;
    private static MainViewModel? _viewModel;

    public static void Configure(IMobilePlatformServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (Gate)
        {
            if (_services is not null)
                throw new InvalidOperationException(
                    "Mobile platform services have already been configured.");

            MobilePlatformSecurityPolicy.EnsureSupported(services.Capabilities);
            _services = services;
        }
    }

    public static IMobilePlatformServices GetRequiredServices()
    {
        lock (Gate)
        {
            return _services
                ?? throw new InvalidOperationException(
                    "The native mobile host did not configure platform services.");
        }
    }

    internal static void Attach(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        lock (Gate)
            _viewModel = viewModel;
    }

    public static void NotifyBackgrounded()
    {
        MainViewModel? viewModel;
        IMobilePlatformServices? services;
        lock (Gate)
        {
            viewModel = _viewModel;
            services = _services;
        }

        if (viewModel is not null)
            _ = ObserveAsync(viewModel.OnBackgroundedAsync());
        if (services is not null)
        {
            _ = ObserveAsync(
                services.ClipboardService.ClearIfOwnedAsync(
                    CancellationToken.None));
        }
    }

    public static void NotifyForegrounded()
    {
        MainViewModel? viewModel;
        lock (Gate)
            viewModel = _viewModel;

        if (viewModel is not null)
            _ = ObserveAsync(viewModel.OnForegroundedAsync());
    }

    public static void NotifyScreenshotTaken()
    {
        MainViewModel? viewModel;
        IMobilePlatformServices? services;
        lock (Gate)
        {
            viewModel = _viewModel;
            services = _services;
        }

        if (viewModel is not null)
            _ = ObserveAsync(LockThenExposeSafeUiAsync(viewModel));
        if (services is not null)
        {
            _ = ObserveAsync(
                services.ClipboardService.ClearIfOwnedAsync(
                    CancellationToken.None));
        }
    }

    private static async Task LockThenExposeSafeUiAsync(
        MainViewModel viewModel)
    {
        await viewModel.OnBackgroundedAsync();
        await viewModel.OnForegroundedAsync();
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Background transitions must remain fail-closed and never crash the host.
        }
    }
}
