using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel(
    IMetadataRepository repository,
    IdentityService identityService,
    SynchronizationService synchronizationService,
    SearchService searchService,
    SecretAccessService secretAccessService,
    WorkspaceService workspaceService,
    IProtectedValueStore protectedValueStore,
    AppSettingsStore settingsStore) : ViewModelBase
{
    private CancellationTokenSource? _activeOperation;

    public ObservableCollection<ConnectedIdentity> Identities { get; } = [];
    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public ObservableCollection<Workspace> Workspaces { get; } = [];
    public IReadOnlyList<string> ObjectTypes { get; } = ["All", "Secret", "Key", "Certificate"];
    public string VersionLabel { get; } = $"Vault Prospector {GetVersion()}";

    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private bool _useCustomClientId;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _identityLabel = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _tenantFilter = string.Empty;
    [ObservableProperty] private string _subscriptionFilter = string.Empty;
    [ObservableProperty] private string _vaultFilter = string.Empty;
    [ObservableProperty] private string _selectedObjectType = "All";
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private bool _staleOnly;
    [ObservableProperty] private bool _enabledOnly;
    [ObservableProperty] private bool _expiredOnly;
    [ObservableProperty] private bool _filterSelectedIdentity;
    [ObservableProperty] private bool _filterSelectedWorkspace;
    [ObservableProperty] private bool _recentlyAccessedFirst;
    [ObservableProperty] private string _statusText = "Starting securely…";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ConnectedIdentity? _selectedIdentity;
    [ObservableProperty] private Workspace? _selectedWorkspace;
    [ObservableProperty] private SearchResultRow? _selectedResult;
    [ObservableProperty] private string _secretPreview = "Select a secret to reveal or copy.";
    [ObservableProperty] private string _workspaceName = string.Empty;
    [ObservableProperty] private bool _offlineCacheEnabled;
    [ObservableProperty] private int _maximumCacheHours = 8;
    [ObservableProperty] private int _clipboardClearSeconds = 30;
    [ObservableProperty] private bool _hasActionableError;
    [ObservableProperty] private string _errorTitle = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _recoveryText = string.Empty;
    [ObservableProperty] private string _errorAnnouncement = string.Empty;

    public bool HasSelectedIdentity => SelectedIdentity is not null;
    public bool HasSelectedWorkspace => SelectedWorkspace is not null;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RunAsync(async cancellationToken =>
        {
            var settings = await settingsStore.LoadAsync(cancellationToken);
            UseCustomClientId = settings.UseCustomClientId;
            ClientId = settings.UseCustomClientId ? settings.ClientId : string.Empty;
            OfflineCacheEnabled = settings.OfflineCacheEnabled;
            MaximumCacheHours = settings.MaximumCacheHours;
            ClipboardClearSeconds = settings.ClipboardClearSeconds;
            await repository.InitializeAsync(cancellationToken);
            await ReloadIdentitiesAsync(cancellationToken);
            await ReloadWorkspacesAsync(cancellationToken);
            await SearchCoreAsync(cancellationToken);
            StatusText = "Ready — metadata search works offline.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private Task AddIdentityAsync() => RunAsync(async cancellationToken =>
    {
        var clientId = EffectiveClientId();
        if (!Guid.TryParse(clientId, out var parsedClientId))
            throw new ArgumentException("A valid Microsoft Entra public-client application ID is required.", nameof(ClientId));
        await SaveSettingsCoreAsync(cancellationToken);
        var identity = await identityService.AddAsync(parsedClientId.ToString("D"), IdentityLabel, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.First(x => x.Id == identity.Id);
        IdentityLabel = string.Empty;
        StatusText = $"Connected {identity.DisplayName}. Select Sync to discover resources.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task RemoveIdentityAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        await identityService.RemoveAsync(SelectedIdentity.Id, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        StatusText = "Identity and its cached tokens were removed.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task SynchronizeAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) throw new InvalidOperationException("Select an identity before synchronizing.");
        StatusText = $"Synchronizing {SelectedIdentity.DisplayName}…";
        var run = await synchronizationService.SynchronizeAsync(SelectedIdentity, cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = $"{run.Status}: {run.VaultCount} vaults and {run.ItemCount} objects; {run.NonSensitiveErrors.Count} isolated errors.";
    });

    [RelayCommand]
    private void CancelOperation()
    {
        _activeOperation?.Cancel();
        StatusText = "Cancelling the active operation…";
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private Task SearchAsync() => RunAsync(SearchCoreAsync);

    [RelayCommand(CanExecute = nameof(CanUseSelectedResult))]
    private Task ToggleFavoriteAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await repository.SetFavoriteAsync(SelectedResult.Id, !SelectedResult.IsFavorite, cancellationToken);
        await SearchCoreAsync(cancellationToken);
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedSecret))]
    private Task RevealAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        using var value = await secretAccessService.RetrieveAsync(SelectedResult.Id, cancellationToken);
        SecretPreview = value.Reveal();
        _ = HideSecretLaterAsync(value.Mask());
        StatusText = "Secret revealed for 10 seconds after Windows Hello verification.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedSecret))]
    private Task CopyAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await secretAccessService.RetrieveAndCopyAsync(SelectedResult.Id, TimeSpan.FromSeconds(Math.Clamp(ClipboardClearSeconds, 5, 300)), CurrentPolicy(), cancellationToken);
        StatusText = $"Copied. Clipboard clears after {Math.Clamp(ClipboardClearSeconds, 5, 300)} seconds if unchanged.";
    });

    [RelayCommand(CanExecute = nameof(CanCacheSelectedSecret))]
    private Task CacheSelectedAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await secretAccessService.RetrieveAndCacheAsync(SelectedResult.Id, SelectedWorkspace?.Id, TimeSpan.FromHours(MaximumCacheHours), CurrentPolicy(), cancellationToken);
        StatusText = $"Encrypted offline copy expires in {MaximumCacheHours} hours.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedSecret))]
    private Task OpenOfflineAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        using var value = await secretAccessService.RetrieveCachedAsync(SelectedResult.Id, cancellationToken);
        SecretPreview = value.Reveal();
        _ = HideSecretLaterAsync(value.Mask());
        StatusText = "Offline secret opened for 10 seconds after Windows Hello verification.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedSecret))]
    private Task PurgeSelectedCachedValueAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await protectedValueStore.PurgeItemAsync(SelectedResult.Id, cancellationToken);
        StatusText = "The selected offline value was purged.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedResult))]
    private Task PurgeSelectedVaultCacheAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await protectedValueStore.PurgeVaultAsync(SelectedResult.VaultId, cancellationToken);
        StatusText = "Offline values for the selected vault were purged.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedWorkspace))]
    private Task PurgeSelectedWorkspaceCacheAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null) return;
        await protectedValueStore.PurgeWorkspaceAsync(SelectedWorkspace.Id, cancellationToken);
        StatusText = "Offline values for the selected workspace were purged.";
    });

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private Task PurgeAllCachedValuesAsync() => RunAsync(async cancellationToken =>
    {
        await protectedValueStore.PurgeAllAsync(cancellationToken);
        StatusText = "All offline values were purged.";
    });

    [RelayCommand(CanExecute = nameof(CanCreateWorkspace))]
    private Task CreateWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        var workspace = new Workspace(Guid.NewGuid(), WorkspaceName.Trim(), string.Empty, Workspaces.Count, CurrentPolicy());
        await workspaceService.SaveAsync(workspace, cancellationToken);
        WorkspaceName = string.Empty;
        await ReloadWorkspacesAsync(cancellationToken);
        StatusText = $"Workspace {workspace.Name} created.";
    });

    [RelayCommand(CanExecute = nameof(CanAddSelectedVaultToWorkspace))]
    private Task AddSelectedVaultToWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null || SelectedResult is null) return;
        await workspaceService.AddResourceAsync(SelectedWorkspace.Id, ResourceLinkType.Vault, SelectedResult.VaultId.ToString("D"), cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = $"Added the selected vault to {SelectedWorkspace.Name}.";
    });

    [RelayCommand(CanExecute = nameof(CanAddSelectedIdentityToWorkspace))]
    private Task AddSelectedIdentityToWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null || SelectedIdentity is null) return;
        await workspaceService.AddResourceAsync(SelectedWorkspace.Id, ResourceLinkType.Identity, SelectedIdentity.Id.ToString("D"), cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = $"Added the selected identity to {SelectedWorkspace.Name}.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedWorkspace))]
    private Task RemoveWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null) return;
        await protectedValueStore.PurgeWorkspaceAsync(SelectedWorkspace.Id, cancellationToken);
        await workspaceService.RemoveAsync(SelectedWorkspace.Id, cancellationToken);
        SelectedWorkspace = null;
        await ReloadWorkspacesAsync(cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = "Workspace and its workspace-scoped offline values were removed.";
    });

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private Task SaveSettingsAsync() => RunAsync(async cancellationToken =>
    {
        await SaveSettingsCoreAsync(cancellationToken);
        StatusText = "Settings saved locally. No client secret is stored.";
    });

    private async Task SearchCoreAsync(CancellationToken cancellationToken)
    {
        VaultObjectType? type = Enum.TryParse<VaultObjectType>(SelectedObjectType, out var parsed) ? parsed : null;
        var results = await searchService.SearchAsync(new SearchRequest(
            SearchText,
            WorkspaceId: FilterSelectedWorkspace ? SelectedWorkspace?.Id : null,
            IdentityId: FilterSelectedIdentity ? SelectedIdentity?.Id : null,
            TenantId: NullIfWhiteSpace(TenantFilter),
            SubscriptionId: NullIfWhiteSpace(SubscriptionFilter),
            VaultName: NullIfWhiteSpace(VaultFilter),
            ObjectType: type,
            Enabled: EnabledOnly ? true : null,
            FavoritesOnly: FavoritesOnly,
            ExpiredOnly: ExpiredOnly,
            StaleOnly: StaleOnly,
            RecentlyAccessedFirst: RecentlyAccessedFirst), cancellationToken);
        SelectedResult = null;
        Results.Clear();
        foreach (var result in results) Results.Add(new SearchResultRow(result));
        StatusText = $"{Results.Count} indexed objects. Values were not retrieved.";
    }

    private async Task ReloadIdentitiesAsync(CancellationToken cancellationToken)
    {
        var selectedIdentityId = SelectedIdentity?.Id;
        Identities.Clear();
        foreach (var identity in await repository.GetIdentitiesAsync(cancellationToken)) Identities.Add(identity);
        SelectedIdentity = selectedIdentityId is null
            ? Identities.FirstOrDefault()
            : Identities.FirstOrDefault(identity => identity.Id == selectedIdentityId) ?? Identities.FirstOrDefault();
        IsFirstRun = Identities.Count == 0;
    }

    private async Task ReloadWorkspacesAsync(CancellationToken cancellationToken)
    {
        Workspaces.Clear();
        foreach (var workspace in await workspaceService.GetAllAsync(cancellationToken)) Workspaces.Add(workspace);
        if (SelectedWorkspace is not null) SelectedWorkspace = Workspaces.FirstOrDefault(x => x.Id == SelectedWorkspace.Id);
    }

    private async Task SaveSettingsCoreAsync(CancellationToken cancellationToken)
    {
        await settingsStore.SaveAsync(new AppSettings(EffectiveClientId(), Math.Clamp(ClipboardClearSeconds, 5, 300), OfflineCacheEnabled, Math.Clamp(MaximumCacheHours, 1, 168), UseCustomClientId), cancellationToken);
    }

    private string EffectiveClientId() => UseCustomClientId ? ClientId.Trim() : ProductIdentity.DefaultClientId;

    private bool CanStartOperation() => !IsBusy;
    private bool CanUseSelectedIdentity() => SelectedIdentity is not null && !IsBusy;
    private bool CanUseSelectedResult() => SelectedResult is not null && !IsBusy;
    private bool CanUseSelectedSecret() => SelectedResult?.Result.Item.ObjectType is VaultObjectType.Secret && !IsBusy;
    private bool CanCacheSelectedSecret() => OfflineCacheEnabled && CanUseSelectedSecret();
    private bool CanUseSelectedWorkspace() => SelectedWorkspace is not null && !IsBusy;
    private bool CanCreateWorkspace() => !string.IsNullOrWhiteSpace(WorkspaceName) && !IsBusy;
    private bool CanAddSelectedVaultToWorkspace() => SelectedWorkspace is not null && SelectedResult is not null && !IsBusy;
    private bool CanAddSelectedIdentityToWorkspace() => SelectedWorkspace is not null && SelectedIdentity is not null && !IsBusy;

    partial void OnSelectedIdentityChanged(ConnectedIdentity? value)
    {
        OnPropertyChanged(nameof(HasSelectedIdentity));
        if (value is null) FilterSelectedIdentity = false;
        RemoveIdentityCommand.NotifyCanExecuteChanged();
        SynchronizeCommand.NotifyCanExecuteChanged();
        AddSelectedIdentityToWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedResultChanged(SearchResultRow? value)
    {
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CacheSelectedCommand.NotifyCanExecuteChanged();
        OpenOfflineCommand.NotifyCanExecuteChanged();
        PurgeSelectedCachedValueCommand.NotifyCanExecuteChanged();
        PurgeSelectedVaultCacheCommand.NotifyCanExecuteChanged();
        AddSelectedVaultToWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedWorkspaceChanged(Workspace? value)
    {
        OnPropertyChanged(nameof(HasSelectedWorkspace));
        if (value is null) FilterSelectedWorkspace = false;
        PurgeSelectedWorkspaceCacheCommand.NotifyCanExecuteChanged();
        AddSelectedVaultToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedIdentityToWorkspaceCommand.NotifyCanExecuteChanged();
        RemoveWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnOfflineCacheEnabledChanged(bool value)
    {
        CacheSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnWorkspaceNameChanged(string value)
    {
        CreateWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        AddIdentityCommand.NotifyCanExecuteChanged();
        RemoveIdentityCommand.NotifyCanExecuteChanged();
        SynchronizeCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CacheSelectedCommand.NotifyCanExecuteChanged();
        OpenOfflineCommand.NotifyCanExecuteChanged();
        PurgeSelectedCachedValueCommand.NotifyCanExecuteChanged();
        PurgeSelectedVaultCacheCommand.NotifyCanExecuteChanged();
        PurgeSelectedWorkspaceCacheCommand.NotifyCanExecuteChanged();
        PurgeAllCachedValuesCommand.NotifyCanExecuteChanged();
        CreateWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedVaultToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedIdentityToWorkspaceCommand.NotifyCanExecuteChanged();
        RemoveWorkspaceCommand.NotifyCanExecuteChanged();
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    private CachePolicy CurrentPolicy() => new(OfflineCacheEnabled, TimeSpan.FromHours(Math.Clamp(MaximumCacheHours, 1, 168)), true, true);

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetVersion()
    {
        var value = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(value) ? "development" : value.Split('+', 2)[0];
    }

    private async Task HideSecretLaterAsync(string mask)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        SecretPreview = mask;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        ClearActionableError();
        IsBusy = true;
        using var operation = new CancellationTokenSource();
        _activeOperation = operation;
        try { await action(operation.Token); }
        catch (OperationCanceledException) { StatusText = "Operation cancelled."; SecretPreview = "Secret hidden."; }
        catch (Exception ex)
        {
            var error = UserFacingErrorMapper.From(ex);
            ErrorTitle = error.Title;
            ErrorMessage = error.Message;
            RecoveryText = error.Recovery;
            HasActionableError = true;
            ErrorAnnouncement = $"{error.Title}. {error.Message} {error.Recovery}";
            SecretPreview = "Secret hidden.";
        }
        finally { _activeOperation = null; IsBusy = false; }
    }

    private void ClearActionableError()
    {
        HasActionableError = false;
        ErrorAnnouncement = string.Empty;
        ErrorTitle = string.Empty;
        ErrorMessage = string.Empty;
        RecoveryText = string.Empty;
    }

}

public sealed class SearchResultRow(SearchResult result)
{
    public SearchResult Result { get; } = result;
    public Guid Id => Result.Item.Id;
    public string Name => Result.Item.ProviderObjectName;
    public string Type => Result.Item.ObjectType.ToString();
    public string Vault => Result.Vault.Name;
    public Guid VaultId => Result.Vault.Id;
    public string Identity => Result.IdentityDisplayName;
    public string Tenant => Result.TenantDisplayName;
    public string Subscription => Result.Vault.SubscriptionId;
    public string State => Result.IsStale ? "Stale" : Result.Item.Enabled ? "Current" : "Disabled";
    public bool IsFavorite => Result.IsFavorite;
    public string FavoriteMarker => IsFavorite ? "★" : "☆";
}
