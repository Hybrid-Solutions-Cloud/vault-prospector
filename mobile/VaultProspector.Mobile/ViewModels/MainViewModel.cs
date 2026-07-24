using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Domain;
using VaultProspector.Mobile.Core;

namespace VaultProspector.Mobile.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMobilePlatformServices _platform;
    private readonly MobileSessionCoordinator _session;
    private readonly IMobileUseCases _useCases;
    private CancellationTokenSource? _revealLifetime;
    private bool _privacyControlsApplied;
    private bool _disposed;

    public MainViewModel(IMobilePlatformServices platform)
        : this(platform, new MobileUseCases(platform))
    {
    }

    public MainViewModel(
        IMobilePlatformServices platform,
        IMobileUseCases useCases)
    {
        _platform = platform;
        _useCases = useCases;
        _session = new MobileSessionCoordinator(
            platform.UserVerificationService,
            platform.SetSensitiveUiCoveredAsync);
        _session.StateChanged += OnSessionStateChanged;
    }

    [ObservableProperty]
    private bool _isLocked = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Starting securely…";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _identityLabel = string.Empty;

    [ObservableProperty]
    private ConnectedIdentity? _selectedIdentity;

    [ObservableProperty]
    private SearchResult? _selectedResult;

    [ObservableProperty]
    private string _revealedValue = string.Empty;

    public ObservableCollection<ConnectedIdentity> Identities { get; } = [];
    public ObservableCollection<SearchResult> Results { get; } = [];

    public bool HasRevealedValue => !string.IsNullOrEmpty(RevealedValue);

    public string PlatformLabel =>
        _platform.Capabilities.Platform == MobilePlatformKind.Android
            ? "Android secure host"
            : "iOS secure host";

    public async Task InitializeAsync()
    {
        try
        {
            await _platform.ApplyPersistentPrivacyControlsAsync(
                CancellationToken.None);
            _privacyControlsApplied = true;
        }
        catch
        {
            StatusText = "Required mobile privacy controls are unavailable.";
            return;
        }

        try
        {
            await _platform.SetSensitiveUiCoveredAsync(
                false,
                CancellationToken.None);
            StatusText = "Locked. Authenticate to open your encrypted local index.";
        }
        catch
        {
            StatusText =
                "Locked. The safe unlock screen remains covered by the platform privacy boundary.";
        }
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        if (!_privacyControlsApplied)
        {
            StatusText = "Required mobile privacy controls are unavailable.";
            return;
        }

        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var unlocked = await _session.UnlockAsync(CancellationToken.None);
            if (!unlocked)
            {
                StatusText = "Unlock was not completed.";
                return;
            }

            using var operation = _session.BeginSensitiveOperation(
                CancellationToken.None);
            await _useCases.InitializeAsync(operation.CancellationToken);
            await ReloadIdentitiesAsync(operation.CancellationToken);
            StatusText = Identities.Count == 0
                ? "Unlocked. Connect your Microsoft Entra identity."
                : "Unlocked. Synchronize an identity or search encrypted metadata.";
        }
        catch
        {
            StatusText = "Unlock failed safely.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LockAsync()
    {
        ClearReveal();
        SearchText = string.Empty;
        Results.Clear();
        await _session.LockAsync(CancellationToken.None);
        StatusText = "Locked. Sensitive state was cleared.";
    }

    public Task OnBackgroundedAsync() => LockAsync();

    public Task OnForegroundedAsync()
    {
        if (!_privacyControlsApplied || !_session.IsLocked)
            return Task.CompletedTask;
        return _platform.SetSensitiveUiCoveredAsync(
            false,
            CancellationToken.None);
    }

    [RelayCommand]
    private async Task ConnectIdentityAsync()
    {
        await RunAsync(async cancellationToken =>
        {
            var identity = await _useCases.ConnectIdentityAsync(
                IdentityLabel,
                cancellationToken);
            await ReloadIdentitiesAsync(CancellationToken.None);
            SelectedIdentity = Identities.FirstOrDefault(
                item => item.Id == identity.Id);
            StatusText = _session.IsLocked
                ? "Identity connected. Unlock again to continue."
                : $"Connected {identity.DisplayName}.";
        });
    }

    [RelayCommand]
    private async Task SynchronizeAsync()
    {
        if (SelectedIdentity is null)
        {
            StatusText = "Select an identity to synchronize.";
            return;
        }

        await RunSensitiveAsync(async cancellationToken =>
        {
            var run = await _useCases.SynchronizeAsync(
                SelectedIdentity,
                cancellationToken);
            StatusText =
                $"Synchronization {run.Status}: {run.VaultCount} vaults, {run.ItemCount} items.";
            await SearchCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private Task SearchAsync() => RunSensitiveAsync(SearchCoreAsync);

    [RelayCommand]
    private async Task RevealAsync()
    {
        if (SelectedResult is null)
        {
            StatusText = "Select a secret to reveal.";
            return;
        }

        await RunSensitiveAsync(async cancellationToken =>
        {
            using var value = await _useCases.RetrieveAsync(
                SelectedResult.Item.Id,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RevealedValue = value.Reveal();
            OnPropertyChanged(nameof(HasRevealedValue));
            StatusText = "Value revealed for 15 seconds.";
            StartRevealExpiry();
        });
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (SelectedResult is null)
        {
            StatusText = "Select a secret to copy.";
            return;
        }

        await RunSensitiveAsync(async cancellationToken =>
        {
            await _useCases.CopyAsync(
                SelectedResult.Item.Id,
                TimeSpan.FromSeconds(30),
                cancellationToken);
            StatusText = "Copied as sensitive content; auto-clear is scheduled for 30 seconds.";
        });
    }

    private async Task SearchCoreAsync(CancellationToken cancellationToken)
    {
        var results = await _useCases.SearchAsync(
            SearchText,
            SelectedIdentity?.Id,
            cancellationToken);
        Results.Clear();
        foreach (var result in results)
            Results.Add(result);
        SelectedResult = Results.FirstOrDefault();
        StatusText = $"{Results.Count} metadata result(s). Values were not retrieved.";
    }

    private async Task ReloadIdentitiesAsync(CancellationToken cancellationToken)
    {
        var identities = await _useCases.GetIdentitiesAsync(cancellationToken);
        var selectedId = SelectedIdentity?.Id;
        Identities.Clear();
        foreach (var identity in identities)
            Identities.Add(identity);
        SelectedIdentity = Identities.FirstOrDefault(
            identity => identity.Id == selectedId) ?? Identities.FirstOrDefault();
    }

    private Task RunSensitiveAsync(Func<CancellationToken, Task> action)
    {
        if (_session.IsLocked)
        {
            StatusText = "Unlock the application first.";
            return Task.CompletedTask;
        }

        return RunAsync(async cancellationToken =>
        {
            using var operation = _session.BeginSensitiveOperation(
                cancellationToken);
            await action(operation.CancellationToken);
        });
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation canceled and sensitive state cleared.";
        }
        catch
        {
            StatusText = "The operation failed safely. No value details were logged.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StartRevealExpiry()
    {
        _revealLifetime?.Cancel();
        _revealLifetime?.Dispose();
        _revealLifetime = new CancellationTokenSource();
        _ = ExpireRevealAsync(_revealLifetime.Token);
    }

    private async Task ExpireRevealAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            ClearReveal();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearReveal()
    {
        _revealLifetime?.Cancel();
        _revealLifetime?.Dispose();
        _revealLifetime = null;
        RevealedValue = string.Empty;
        OnPropertyChanged(nameof(HasRevealedValue));
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) =>
        IsLocked = _session.IsLocked;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearReveal();
        _session.StateChanged -= OnSessionStateChanged;
        _session.Dispose();
        _useCases.Dispose();
        GC.SuppressFinalize(this);
    }
}
