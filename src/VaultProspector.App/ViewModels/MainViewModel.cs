using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Platform;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel(
    IMetadataRepository repository,
    IdentityService identityService,
    SynchronizationService synchronizationService,
    SearchService searchService,
    SecretAccessService secretAccessService,
    WorkspaceService workspaceService,
    IProtectedValueStore protectedValueStore,
    AppSettingsStore settingsStore,
    IUserVerificationService verificationService,
    LocalDataRecoveryService localDataRecoveryService,
    IManagedIdentityEnvironmentDetector managedIdentityEnvironmentDetector,
    IWorkloadIdentityAdministrationService? workloadIdentityAdministrationService = null,
    ILocalEncryptionRotationEngine? localEncryptionRotationEngine = null,
    LocalRecoveryArchiveService? localRecoveryArchiveService = null,
    BrowserFillService? browserFillService = null,
    CyberArkService? cyberArkService = null,
    IEnterprisePolicy? enterprisePolicy = null,
    ISupportBundleService? supportBundleService = null,
    IRevealVerificationSession? revealVerificationSession = null,
    IReleaseUpdateService? releaseUpdateService = null,
    IBrowserIntegrationDiagnostics? browserIntegrationDiagnostics = null,
    GovernedAzureMutationService? governedAzureMutationService = null) : ViewModelBase
{
    private static readonly IdentityType[] SupportedIdentityTypes =
    [
        IdentityType.InteractiveUser,
        IdentityType.ManagedIdentity,
        IdentityType.ServicePrincipal,
        IdentityType.FederatedServicePrincipal,
    ];
    private readonly bool _isCyberArkPreviewEnabled =
        AppContext.TryGetSwitch("VaultProspector.EnableCyberArkPreview", out var cyberArkPreviewEnabled) &&
        cyberArkPreviewEnabled;
    private CancellationTokenSource? _activeOperation;
    private bool _isReloadingIdentities;
    private bool _managedIdentityHostSupported;
    private int _subscriptionLoadVersion;
    private int _sensitivePresentationEpoch;
    private readonly List<WorkloadIdentityCandidate>
        _allWorkloadIdentityCandidates = [];
    private readonly List<DiagnosticEvent>
        _allDiagnosticEvents = [];

    public ObservableCollection<ConnectedIdentity> Identities { get; } = [];
    public ObservableCollection<TenantAccess> Tenants { get; } = [];
    public ObservableCollection<SubscriptionSelectionRow> Subscriptions { get; } = [];
    public ObservableCollection<VaultAccessRow> VaultAccessPaths { get; } = [];
    public ObservableCollection<WorkloadIdentityCandidateRow> WorkloadIdentityCandidates { get; } = [];
    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public ObservableCollection<SyncErrorRow> SyncErrors { get; } = [];
    public ObservableCollection<DiagnosticEventRow> DiagnosticEvents { get; } = [];
    public ObservableCollection<Workspace> Workspaces { get; } = [];
    public ObservableCollection<LocalRecoveryArchiveRow> RecoveryArchives { get; } = [];
    public ObservableCollection<SearchFilterOption> TenantFilterOptions { get; } =
        [SearchFilterOption.All("All tenants")];
    public ObservableCollection<SearchFilterOption> SubscriptionFilterOptions { get; } =
        [SearchFilterOption.All("All subscriptions")];
    public ObservableCollection<SearchFilterOption> VaultFilterOptions { get; } =
        [SearchFilterOption.All("All vaults")];
    public IReadOnlyList<string> ObjectTypes { get; } = ["All", "Secret", "Key", "Certificate"];
    public IReadOnlyList<CloseBehavior> CloseBehaviors { get; } =
        [CloseBehavior.Ask, CloseBehavior.Exit, CloseBehavior.LockToNotificationArea];
    public IReadOnlyList<RevealVerificationGraceOption> RevealVerificationGraceOptions { get; } =
        RevealVerificationGraceOption.All;
    public string VersionLabel { get; } = $"Vault Prospector {GetVersion()}";
    public bool IsCyberArkPreviewEnabled => _isCyberArkPreviewEnabled;
    public string DiagnosticLogPath =>
        supportBundleService?.DiagnosticLogPath ??
        "Diagnostic log path is unavailable in this build.";
    public string ActiveWorkspaceContext =>
        SelectedWorkspace is null
            ? "Workspace: all discovered sources"
            : $"Workspace: {SelectedWorkspace.Name}";
    public string ActiveIdentityContext =>
        SelectedIdentity is null
            ? "Identity: none selected"
            : $"Selected identity: {(string.IsNullOrWhiteSpace(
                SelectedIdentity.DisplayName)
                ? SelectedIdentity.UsernameHint
                : SelectedIdentity.DisplayName)}";
    public string ActiveSubscriptionContext =>
        SelectedSubscription is null
            ? "Subscription filter: all"
            : $"Selected subscription: {SelectedSubscription.DisplayName}";

    [ObservableProperty] private IdentityType _selectedIdentityType = IdentityType.InteractiveUser;
    [ObservableProperty] private string _credentialData = string.Empty;
    [ObservableProperty] private string _replacementCredentialData = string.Empty;
    [ObservableProperty] private string _tenantId = string.Empty;
    [ObservableProperty] private string _administrationSubscriptionId = string.Empty;
    [ObservableProperty] private string _administrationResourceGroup = string.Empty;
    [ObservableProperty] private string _administrationIdentityName = string.Empty;
    [ObservableProperty] private string _workloadIdentitySearchText = string.Empty;
    [ObservableProperty]
    private string _workloadIdentityFilterStatus =
        "Run a discovery action to list eligible customer-managed identities.";
    [ObservableProperty] private string _administrationVaultResourceId = string.Empty;
    [ObservableProperty] private string _administrationRoleDefinitionId = string.Empty;
    [ObservableProperty]
    private string _administrationPlanText =
        "No plan generated. Preview is read-only; Azure mutation remains disabled by application policy.";
    [ObservableProperty]
    private string _managedIdentityAvailabilityText =
        "Managed identity availability is checked after local unlock.";
    [ObservableProperty]
    private string _identityRemovalConfirmation = string.Empty;
    public ObservableCollection<IdentityType> IdentityTypes { get; } =
        [
            IdentityType.InteractiveUser,
            IdentityType.ServicePrincipal,
            IdentityType.FederatedServicePrincipal,
        ];

    public bool IsInteractiveUser => SelectedIdentityType == IdentityType.InteractiveUser;
    public bool IsManagedIdentity => SelectedIdentityType == IdentityType.ManagedIdentity;
    public bool IsServicePrincipal => SelectedIdentityType == IdentityType.ServicePrincipal;
    public bool IsFederatedServicePrincipal =>
        SelectedIdentityType == IdentityType.FederatedServicePrincipal;
    public bool IsWorkloadIdentity => SelectedIdentityType != IdentityType.InteractiveUser;
    public bool SelectedIdentitySupportsCredentialRotation =>
        SelectedIdentity?.Type is IdentityType.ServicePrincipal or IdentityType.FederatedServicePrincipal;
    public string CredentialRotationLabel =>
        SelectedIdentity?.Type == IdentityType.FederatedServicePrincipal
            ? "Replacement federated token file path"
            : "Replacement certificate thumbprint";
    public string ConnectIdentityActionText => SelectedIdentityType switch
    {
        IdentityType.InteractiveUser => "Continue to Microsoft sign-in",
        IdentityType.ManagedIdentity => "Verify and connect managed identity",
        IdentityType.ServicePrincipal => "Verify and connect certificate identity",
        IdentityType.FederatedServicePrincipal => "Verify and connect federated identity",
        _ => "Connect identity",
    };

    partial void OnSelectedIdentityTypeChanged(IdentityType value)
    {
        OnPropertyChanged(nameof(IsInteractiveUser));
        OnPropertyChanged(nameof(IsManagedIdentity));
        OnPropertyChanged(nameof(IsServicePrincipal));
        OnPropertyChanged(nameof(IsFederatedServicePrincipal));
        OnPropertyChanged(nameof(IsWorkloadIdentity));
        OnPropertyChanged(nameof(ConnectIdentityActionText));
        AddIdentityCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private string _clientId = string.Empty;
    [ObservableProperty] private bool _useCustomClientId;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private int _selectedMainTabIndex;
    [ObservableProperty] private string _identityLabel = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _tenantFilter = string.Empty;
    [ObservableProperty] private string _subscriptionFilter = string.Empty;
    [ObservableProperty] private string _vaultFilter = string.Empty;
    [ObservableProperty] private SearchFilterOption? _selectedTenantFilterOption;
    [ObservableProperty] private SearchFilterOption? _selectedSubscriptionFilterOption;
    [ObservableProperty] private SearchFilterOption? _selectedVaultFilterOption;
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
    [ObservableProperty] private string _activeOperationText = string.Empty;
    [ObservableProperty] private string _setupSyncStatus = "Not started";
    [ObservableProperty] private ConnectedIdentity? _selectedIdentity;
    [ObservableProperty] private SyncErrorRow? _selectedSyncError;
    [ObservableProperty] private TenantAccess? _selectedTenant;
    [ObservableProperty] private SubscriptionSelectionRow? _selectedSubscription;
    [ObservableProperty] private VaultAccessRow? _selectedVaultAccess;
    [ObservableProperty]
    private WorkloadIdentityCandidateRow? _selectedWorkloadIdentityCandidate;
    [ObservableProperty] private Workspace? _selectedWorkspace;
    [ObservableProperty] private bool _workspaceCacheEnabled;
    [ObservableProperty] private int _workspaceMaximumCacheHours = 8;
    [ObservableProperty] private bool _workspaceAllowClipboard = true;
    [ObservableProperty] private CloseBehavior _selectedCloseBehavior = CloseBehavior.Ask;
    [ObservableProperty] private bool _backgroundMetadataSyncEnabled;
    [ObservableProperty] private bool _minimizeToNotificationArea = true;
    [ObservableProperty]
    private RevealVerificationGraceOption
        _selectedRevealVerificationGrace =
            RevealVerificationGraceOption.Off;
    [ObservableProperty] private bool _isCloseChoiceVisible;
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
    [ObservableProperty] private bool _isUnlocked;
    [ObservableProperty] private bool _isApplicationReady;
    [ObservableProperty] private bool _isLocalDataBlocked;
    [ObservableProperty] private bool _canResetLocalData;
    [ObservableProperty] private bool _isRecoveryComplete;
    [ObservableProperty] private string _unlockMessage = "Use Windows Hello to unlock local Vault Prospector data.";
    [ObservableProperty] private string _unlockRecovery = string.Empty;
    [ObservableProperty] private string _localDataFailureTitle = string.Empty;
    [ObservableProperty] private string _localDataRecoveryGuidance = string.Empty;
    [ObservableProperty] private string _resetConfirmation = string.Empty;
    [ObservableProperty] private string _recoveryArchivePath = string.Empty;
    [ObservableProperty] private LocalRecoveryArchiveRow? _selectedRecoveryArchive;
    [ObservableProperty] private string _recoveryArchiveDeleteConfirmation = string.Empty;
    [ObservableProperty]
    private string _recoveryArchiveStatus =
        "Recovery archives remain local until you explicitly delete them.";
    [ObservableProperty]
    private string _enterprisePolicyStatus =
        "No machine-managed enterprise policy is configured.";
    [ObservableProperty]
    private string _supportBundleStatus =
        "No support bundle has been created in this session.";
    [ObservableProperty] private string _latestSupportBundlePath = string.Empty;
    [ObservableProperty]
    private string _diagnosticViewerStatus =
        "Select Refresh diagnostics to display recent privacy-safe events.";
    [ObservableProperty]
    private string _diagnosticSearchText = string.Empty;

    public bool HasSelectedIdentity => SelectedIdentity is not null;
    public bool IsSelectedIdentityEnabled => SelectedIdentity?.IsEnabled == true;
    public bool IsSelectedIdentityDisabled => SelectedIdentity?.IsEnabled == false;
    public bool IsManagedIdentityHostSupported => _managedIdentityHostSupported;
    public bool HasSelectedWorkspace => SelectedWorkspace is not null;
    public bool HasSyncErrors => SyncErrors.Count > 0;
    public string SetupConnectionsStatus =>
        Identities.Count == 0 ? "Current step" : "Complete";
    public string SetupScopeStatus =>
        Subscriptions.Count == 0 && VaultAccessPaths.Count == 0
            ? Identities.Count == 0 ? "Waiting for identity" : "Ready to synchronize"
            : "Complete";
    public bool IsEnterpriseOfflineCacheAllowed =>
        EnterprisePolicy().AllowOfflineCache;
    public bool IsEnterpriseClipboardAllowed =>
        EnterprisePolicy().AllowClipboard;
    public string RevealVerificationGraceStatus
    {
        get
        {
            var requested = TimeSpan.FromSeconds(
                SelectedRevealVerificationGrace.Seconds);
            var effective = EnterprisePolicy()
                .ConstrainRevealVerificationGracePeriod(requested);
            return effective <= TimeSpan.Zero
                ? "Off. Every explicit reveal requires Windows verification."
                : $"Up to {(int)effective.TotalSeconds} seconds after a successful verification. " +
                  "Every secret still requires an explicit Reveal action and remains visible for at most 10 seconds.";
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RunAsync(async cancellationToken =>
        {
            if (IsRecoveryComplete)
            {
                StatusText = "Local data was archived. Close and reopen Vault Prospector to continue.";
                return;
            }

            IsApplicationReady = false;
            if (!IsUnlocked)
            {
                if (!verificationService.IsAvailable)
                {
                    UnlockMessage = "Vault Prospector remains locked because Windows verification is unavailable.";
                    UnlockRecovery = "Use a supported Windows 10 or Windows 11 interactive session and configure Windows Hello.";
                    StatusText = "Application locked — Windows verification unavailable.";
                    return;
                }

                StatusText = "Waiting for local verification...";
                var verificationResult = await verificationService.VerifyAsync("Unlock Vault Prospector", cancellationToken);
                if (verificationResult != UserVerificationResult.Verified)
                {
                    ConfigureUnlockFailure(verificationResult);
                    return;
                }

                IsUnlocked = true;
                UnlockMessage = "Local verification completed.";
                UnlockRecovery = string.Empty;
            }

            var recoveredInterruptedRotation = false;
            if (localEncryptionRotationEngine is not null)
            {
                try
                {
                    var recovery = await localEncryptionRotationEngine
                        .RecoverIfNeededAsync(cancellationToken);
                    recoveredInterruptedRotation = recovery.Recovered;
                }
                catch (Exception ex) when (IsProtectedLocalDataFailure(ex))
                {
                    ConfigureLocalDataFailure(ex);
                    return;
                }
            }

            var settings = await settingsStore.LoadAsync(cancellationToken);
            UseCustomClientId = settings.UseCustomClientId;
            ClientId = settings.UseCustomClientId ? settings.ClientId : string.Empty;
            OfflineCacheEnabled = settings.OfflineCacheEnabled;
            MaximumCacheHours = settings.MaximumCacheHours;
            ClipboardClearSeconds = settings.ClipboardClearSeconds;
            SelectedCloseBehavior = settings.CloseBehavior;
            BackgroundMetadataSyncEnabled = settings.BackgroundMetadataSyncEnabled;
            MinimizeToNotificationArea = settings.MinimizeToNotificationArea;
            SelectedRevealVerificationGrace =
                RevealVerificationGraceOptions.FirstOrDefault(
                    option =>
                        option.Seconds ==
                        settings.RevealVerificationGraceSeconds) ??
                RevealVerificationGraceOption.Off;
            ApplyEnterprisePolicyToPreferences();
            await ConfigureManagedIdentityAvailabilityAsync(cancellationToken);

            try
            {
                await repository.InitializeAsync(cancellationToken);
            }
            catch (Exception ex) when (IsProtectedLocalDataFailure(ex))
            {
                ConfigureLocalDataFailure(ex);
                return;
            }

            IsLocalDataBlocked = false;
            CanResetLocalData = false;
            await ReloadIdentitiesAsync(cancellationToken);
            await ReloadWorkspacesAsync(cancellationToken);
            await RefreshSearchFilterOptionsAsync(cancellationToken);
            await SearchCoreAsync(cancellationToken);
            await ReloadBrowserIntegrationAsync(cancellationToken);
            await ReloadCyberArkProfilesAsync(cancellationToken);
            IsApplicationReady = true;
            await ReloadRecoveryArchivesCoreAsync(cancellationToken);
            StatusText = recoveredInterruptedRotation
                ? "Ready — an interrupted local encryption rotation was rolled back to its verified pre-rotation archive."
                : IsFirstRun
                    ? "Local unlock complete — connect your first Microsoft Entra identity to begin."
                    : "Ready — metadata search works offline.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanArchiveAndResetLocalData))]
    private Task ArchiveAndResetLocalDataAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var archive = await localDataRecoveryService.ArchiveAndResetAsync(ResetConfirmation, cancellationToken);
            RecoveryArchivePath = archive.HadExistingData
                ? archive.ArchivePath
                : "No previous local data was present.";
            ResetConfirmation = string.Empty;
            CanResetLocalData = false;
            IsRecoveryComplete = true;
            IsApplicationReady = false;
            StatusText = "Encrypted local state was archived. Close and reopen Vault Prospector to create fresh local state.";
        });
    }

    private bool CanArchiveAndResetLocalData() =>
        IsLocalDataBlocked &&
        CanResetLocalData &&
        !IsRecoveryComplete &&
        string.Equals(ResetConfirmation.Trim(), LocalDataRecoveryService.ConfirmationPhrase, StringComparison.Ordinal);

    partial void OnResetConfirmationChanged(string value) =>
        ArchiveAndResetLocalDataCommand.NotifyCanExecuteChanged();

    partial void OnCanResetLocalDataChanged(bool value) =>
        ArchiveAndResetLocalDataCommand.NotifyCanExecuteChanged();

    partial void OnIsRecoveryCompleteChanged(bool value) =>
        ArchiveAndResetLocalDataCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanAddIdentity))]
    private Task AddIdentityAsync() => RunAsync(async cancellationToken =>
    {
        var clientId = SelectedIdentityType == IdentityType.InteractiveUser
            ? EffectiveClientId()
            : ClientId.Trim();
        await SaveSettingsCoreAsync(cancellationToken);

        ConnectedIdentity identity;
        if (SelectedIdentityType == IdentityType.InteractiveUser)
        {
            if (!Guid.TryParse(clientId, out var parsedClientId))
                throw new ArgumentException("A valid Microsoft Entra public-client application ID is required.", nameof(ClientId));
            identity = await identityService.AddAsync(parsedClientId.ToString("D"), IdentityLabel, cancellationToken);
        }
        else
        {
            if (SelectedIdentityType == IdentityType.ManagedIdentity &&
                !_managedIdentityHostSupported)
            {
                throw new WorkloadIdentityConfigurationException(
                    "Managed identity is unavailable on this host.",
                    nameof(SelectedIdentityType));
            }
            identity = await identityService.AddWorkloadIdentityAsync(clientId, TenantId, IdentityLabel, SelectedIdentityType, CredentialData, cancellationToken);
        }

        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.First(x => x.Id == identity.Id);
        IdentityLabel = string.Empty;
        CredentialData = string.Empty;
        TenantId = string.Empty;
        StatusText = $"Connected {identity.DisplayName}. Select Sync to discover resources.";
    }, "Connecting an identity");

    [RelayCommand(CanExecute = nameof(CanRemoveIdentity))]
    private Task RemoveIdentityAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityName = SelectedIdentity.DisplayName;
        await identityService.RemoveAsync(SelectedIdentity.Id, cancellationToken);
        IdentityRemovalConfirmation = string.Empty;
        await ReloadIdentitiesAsync(cancellationToken);
        StatusText = $"The local connection for {identityName} and its cached tokens were removed. The Microsoft Entra account was not deleted.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task PurgeSelectedIdentityCacheAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var purgedVaultCount = await identityService.PurgeOfflineValuesAsync(
            SelectedIdentity.Id,
            cancellationToken);
        StatusText =
            $"Offline value storage was cleared for {purgedVaultCount} vault scope(s) associated with {SelectedIdentity.DisplayName}.";
    });

    [RelayCommand(CanExecute = nameof(CanDisableIdentity))]
    private Task DisableIdentityAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityId = SelectedIdentity.Id;
        var identityName = SelectedIdentity.DisplayName;
        await identityService.DisableAsync(identityId, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.FirstOrDefault(identity => identity.Id == identityId);
        StatusText = $"Identity {identityName} is now disabled. Select Enable to use it again.";
    });

    [RelayCommand(CanExecute = nameof(CanEnableIdentity))]
    private Task EnableIdentityAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityId = SelectedIdentity.Id;
        var identityName = SelectedIdentity.DisplayName;
        await identityService.EnableAsync(identityId, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.FirstOrDefault(identity => identity.Id == identityId);
        StatusText = $"Identity {identityName} is enabled and ready to use.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task ReauthenticateIdentityAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        await identityService.ReauthenticateAsync(SelectedIdentity.Id, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        StatusText = $"Identity {SelectedIdentity.DisplayName} reauthenticated successfully.";
    });

    [RelayCommand(CanExecute = nameof(CanRotateSelectedCredential))]
    private Task RotateSelectedCredentialAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityId = SelectedIdentity.Id;
        var identityName = SelectedIdentity.DisplayName;
        await identityService.RotateWorkloadCredentialAsync(
            identityId,
            ReplacementCredentialData,
            cancellationToken);
        ReplacementCredentialData = string.Empty;
        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.First(identity => identity.Id == identityId);
        StatusText = $"Credential for {identityName} was validated and rotated.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task RevokeIdentityAccessAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityId = SelectedIdentity.Id;
        var identityName = SelectedIdentity.DisplayName;
        try
        {
            var result = await identityService.RevokeLocalAccessAsync(
                identityId,
                cancellationToken);
            ReplacementCredentialData = string.Empty;
            StatusText = result.ProviderCredentialRemoved
                ? $"Local access for {identityName} was revoked and {result.PurgedVaultCount} associated offline vault cache(s) were purged. Revoke the external credential at its issuer if it may be compromised."
                : $"Local access for {identityName} was revoked and {result.PurgedVaultCount} associated offline vault cache(s) were purged. Provider token cleanup could not be confirmed; revoke the external credential at its issuer.";
        }
        finally
        {
            await ReloadIdentitiesAsync(CancellationToken.None);
            SelectedIdentity = Identities.FirstOrDefault(identity => identity.Id == identityId);
        }
    });

    [RelayCommand(CanExecute = nameof(CanAdministerWorkloadIdentities))]
    private Task AuthorizeDirectoryReadAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        var identityId = SelectedIdentity.Id;
        await identityService.AuthorizeDirectoryReadAsync(identityId, cancellationToken);
        await ReloadIdentitiesAsync(cancellationToken);
        SelectedIdentity = Identities.First(identity => identity.Id == identityId);
        WorkloadIdentityFilterStatus =
            "Directory access authorized. Select List service principals to load eligible customer-managed applications.";
        StatusText = WorkloadIdentityFilterStatus;
    });

    [RelayCommand(CanExecute = nameof(CanDiscoverManagedIdentities))]
    private Task DiscoverManagedIdentitiesAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null || workloadIdentityAdministrationService is null) return;
        var candidates = await workloadIdentityAdministrationService.ListManagedIdentitiesAsync(
            SelectedIdentity,
            AdministrationSubscriptionId,
            cancellationToken);
        ReplaceWorkloadCandidates(candidates);
        WorkloadIdentityFilterStatus =
            $"{candidates.Count} user-assigned managed identities loaded from the exact subscription. No Azure resources were changed.";
        StatusText = WorkloadIdentityFilterStatus;
    });

    [RelayCommand(CanExecute = nameof(CanDiscoverServicePrincipals))]
    private Task DiscoverServicePrincipalsAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null || workloadIdentityAdministrationService is null) return;
        var candidates = await workloadIdentityAdministrationService.ListServicePrincipalsAsync(
            SelectedIdentity,
            cancellationToken);
        ReplaceWorkloadCandidates(candidates);
        WorkloadIdentityFilterStatus =
            $"{candidates.Count} enabled, customer-owned application service principals loaded. Microsoft first-party and foreign-tenant principals were excluded. No Azure resources were changed.";
        StatusText = WorkloadIdentityFilterStatus;
    });

    [RelayCommand(CanExecute = nameof(CanAssessWorkloadIdentityPermissions))]
    private Task AssessWorkloadIdentityPermissionsAsync() => RunAsync(
        async cancellationToken =>
        {
            if (SelectedIdentity is null ||
                SelectedWorkloadIdentityCandidate is null ||
                workloadIdentityAdministrationService is null)
            {
                return;
            }

            var selectedRow = SelectedWorkloadIdentityCandidate;
            var assessed = await workloadIdentityAdministrationService
                .AssessPermissionsAsync(
                    SelectedIdentity,
                    selectedRow.Candidate,
                    AdministrationVaultResourceId,
                    cancellationToken);
            var sourceIndex =
                _allWorkloadIdentityCandidates.FindIndex(
                    candidate =>
                        string.Equals(
                            candidate.PrincipalId,
                            assessed.PrincipalId,
                            StringComparison.OrdinalIgnoreCase));
            if (sourceIndex >= 0)
            {
                _allWorkloadIdentityCandidates[sourceIndex] =
                    assessed;
            }
            else
            {
                _allWorkloadIdentityCandidates.Add(assessed);
            }

            ApplyWorkloadIdentityFilter(
                assessed.PrincipalId);
            StatusText =
                $"Read-only authorization evidence refreshed for {assessed.DisplayName} at the exact Key Vault. No Azure resources or values were changed.";
        });

    [RelayCommand(CanExecute = nameof(CanPreviewManagedIdentity))]
    private Task PreviewManagedIdentityAsync() => RunAsync(cancellationToken =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SelectedIdentity is null || workloadIdentityAdministrationService is null)
            return Task.CompletedTask;
        var plan = workloadIdentityAdministrationService.BuildManagedIdentityDryRun(
            SelectedIdentity.HomeTenantId,
            AdministrationSubscriptionId,
            AdministrationResourceGroup,
            AdministrationIdentityName,
            NullIfWhiteSpace(AdministrationVaultResourceId),
            NullIfWhiteSpace(AdministrationRoleDefinitionId));
        AdministrationPlanText = FormatPlan(plan);
        StatusText = "Managed-identity preview generated. Azure mutation remains disabled.";
        return Task.CompletedTask;
    });

    [RelayCommand(CanExecute = nameof(CanPreviewServicePrincipal))]
    private Task PreviewServicePrincipalAsync() => RunAsync(cancellationToken =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SelectedIdentity is null || workloadIdentityAdministrationService is null)
            return Task.CompletedTask;
        var plan = workloadIdentityAdministrationService.BuildServicePrincipalDryRun(
            SelectedIdentity.HomeTenantId,
            AdministrationIdentityName,
            NullIfWhiteSpace(AdministrationVaultResourceId),
            NullIfWhiteSpace(AdministrationRoleDefinitionId));
        AdministrationPlanText = FormatPlan(plan);
        StatusText = "Service-principal preview generated. Azure mutation remains disabled.";
        return Task.CompletedTask;
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentityOnline))]
    private Task SynchronizeAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) throw new InvalidOperationException("Select an identity before synchronizing.");
        StatusText = $"Synchronizing {SelectedIdentity.DisplayName}…";
        var run = await synchronizationService.SynchronizeAsync(SelectedIdentity, cancellationToken);
        await ReloadSubscriptionsCoreAsync(SelectedIdentity.Id, cancellationToken);
        await RefreshSearchFilterOptionsAsync(cancellationToken);
        await SearchCoreAsync(cancellationToken);
        ReplaceSyncErrors(run);
        SetupSyncStatus = run.Status switch
        {
            SyncStatus.Completed => "Complete",
            SyncStatus.CompletedWithErrors => "Complete with isolated errors",
            SyncStatus.Cancelled => "Cancelled",
            _ => "Needs attention",
        };
        ContinueToSearchCommand.NotifyCanExecuteChanged();
        StatusText = $"{run.Status}: {run.VaultCount} vaults and {run.ItemCount} objects; {run.NonSensitiveErrors.Count} isolated errors.";
    }, $"Synchronizing {SelectedIdentity?.DisplayName ?? "the selected identity"}");

    [RelayCommand(CanExecute = nameof(CanContinueToSearch))]
    private void ContinueToSearch()
    {
        IsFirstRun = false;
        SelectedMainTabIndex = 0;
        StatusText = HasSyncErrors
            ? "Search is ready. Successful metadata is available; isolated synchronization errors can be reviewed and retried from Identities."
            : "Search is ready.";
    }

    [RelayCommand(CanExecute = nameof(CanRetrySelectedSyncError))]
    private Task RetrySelectedSyncErrorAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null || SelectedSyncError is null)
            return;

        StatusText = $"Retrying {SelectedSyncError.Scope}…";
        var run = await synchronizationService.RetryFailedScopesAsync(
            SelectedIdentity,
            [SelectedSyncError.Detail],
            cancellationToken);
        await ReloadSubscriptionsCoreAsync(
            SelectedIdentity.Id,
            cancellationToken);
        await RefreshSearchFilterOptionsAsync(cancellationToken);
        await SearchCoreAsync(cancellationToken);
        ReplaceSyncErrors(run);
        SetupSyncStatus = run.Status == SyncStatus.Completed
            ? "Complete"
            : "Complete with isolated errors";
        ContinueToSearchCommand.NotifyCanExecuteChanged();
        StatusText = run.NonSensitiveErrors.Count == 0
            ? $"Retry completed: {run.VaultCount} vaults and {run.ItemCount} objects refreshed."
            : $"Retry completed with {run.NonSensitiveErrors.Count} isolated errors. Successful results remain available.";
    }, $"Retrying {SelectedSyncError?.Scope ?? "the selected failed scope"}");

    [RelayCommand(CanExecute = nameof(CanUseSelectedIdentity))]
    private Task RefreshSubscriptionsAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null) return;
        await ReloadSubscriptionsCoreAsync(SelectedIdentity.Id, cancellationToken);
        StatusText = $"{Subscriptions.Count} discovered subscriptions loaded for {SelectedIdentity.DisplayName}.";
    });

    [RelayCommand(CanExecute = nameof(CanExcludeSubscription))]
    private Task ExcludeSubscriptionAsync() => SetSelectedSubscriptionStateAsync(false);

    [RelayCommand(CanExecute = nameof(CanIncludeSubscription))]
    private Task IncludeSubscriptionAsync() => SetSelectedSubscriptionStateAsync(true);

    private Task SetSelectedSubscriptionStateAsync(bool isSelected) => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null || SelectedSubscription is null) return;
        var subscriptionId = SelectedSubscription.Id;
        var displayName = SelectedSubscription.DisplayName;
        await repository.SetSubscriptionSelectedAsync(subscriptionId, isSelected, cancellationToken);
        await ReloadSubscriptionsCoreAsync(SelectedIdentity.Id, cancellationToken);
        SelectedSubscription = Subscriptions.FirstOrDefault(subscription => subscription.Id == subscriptionId);
        StatusText = isSelected
            ? $"{displayName} will be included in future synchronization."
            : $"{displayName} will be excluded from future synchronization. Existing local metadata remains searchable until a complete synchronization reconciles it.";
    });

    [RelayCommand(CanExecute = nameof(CanExcludeVault))]
    private Task ExcludeVaultAsync() => SetSelectedVaultStateAsync(false);

    [RelayCommand(CanExecute = nameof(CanIncludeVault))]
    private Task IncludeVaultAsync() => SetSelectedVaultStateAsync(true);

    private Task SetSelectedVaultStateAsync(bool isSelected) => RunAsync(async cancellationToken =>
    {
        if (SelectedIdentity is null || SelectedVaultAccess is null) return;
        var accessId = SelectedVaultAccess.Id;
        var vaultName = SelectedVaultAccess.Vault;
        await repository.SetVaultSelectedAsync(accessId, isSelected, cancellationToken);
        await ReloadSubscriptionsCoreAsync(SelectedIdentity.Id, cancellationToken);
        SelectedVaultAccess = VaultAccessPaths.FirstOrDefault(access => access.Id == accessId);
        StatusText = isSelected
            ? $"{vaultName} will be included in future synchronization."
            : $"{vaultName} will be excluded before future metadata enumeration. Existing local metadata remains searchable until a complete synchronization reconciles it.";
    });

    [RelayCommand]
    private void CancelOperation()
    {
        _activeOperation?.Cancel();
        StatusText = $"Cancelling {ActiveOperationText}…";
    }

    public event EventHandler? ExitRequested;
    public event EventHandler? ContinueInBackgroundRequested;

    [RelayCommand]
    private void RequestExit()
    {
        IsCloseChoiceVisible = false;
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ContinueInBackground()
    {
        IsCloseChoiceVisible = false;
        ContinueInBackgroundRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelClose() => IsCloseChoiceVisible = false;

    public void RequestCloseChoice() => IsCloseChoiceVisible = true;

    [RelayCommand]
    private void LockNow()
    {
        revealVerificationSession?.Invalidate();
        ClearBrowserDestinationCapture();
        CancelPendingBrowserFill(
            "Browser fill was cancelled because Vault Prospector was locked.");
        CancelPendingGovernedMutation();
        _sensitivePresentationEpoch++;
        _activeOperation?.Cancel();
        SecretPreview = "Secret hidden.";
        HideCyberArkValue();
        IsUnlocked = false;
        IsApplicationReady = false;
        StatusText = "Application locked manually.";
    }

    public void LockForBackground()
    {
        revealVerificationSession?.Invalidate();
        ClearBrowserDestinationCapture();
        CancelPendingBrowserFill("Browser fill was cancelled because Vault Prospector moved to the notification area.");
        CancelPendingGovernedMutation();
        _sensitivePresentationEpoch++;
        _activeOperation?.Cancel();
        SecretPreview = "Secret hidden.";
        HideCyberArkValue();
        IsUnlocked = false;
        IsApplicationReady = false;
        StatusText = "Locked in the notification area. Metadata-only background sync may continue if enabled.";
    }

    public void LockForSystemBoundary()
    {
        revealVerificationSession?.Invalidate();
        ClearBrowserDestinationCapture();
        CancelPendingBrowserFill("Browser fill was cancelled by a Windows security boundary.");
        CancelPendingGovernedMutation();
        _sensitivePresentationEpoch++;
        _activeOperation?.Cancel();
        IsCloseChoiceVisible = false;
        SecretPreview = "Secret hidden.";
        HideCyberArkValue();
        IsUnlocked = false;
        IsApplicationReady = false;
        StatusText = "Locked after a Windows session or power transition.";
    }

    public Task BackgroundSynchronizeOnceAsync()
    {
        if (!BackgroundMetadataSyncEnabled ||
            IsBusy ||
            SelectedIdentity is not { IsEnabled: true, AuthenticationState: AuthenticationState.Ready })
            return Task.CompletedTask;

        return RunAsync(async cancellationToken =>
        {
            var run = await synchronizationService.SynchronizeAsync(SelectedIdentity, cancellationToken);
            StatusText = run.Status switch
            {
                SyncStatus.Completed => $"Background metadata sync completed: {run.VaultCount} vaults, {run.ItemCount} objects.",
                SyncStatus.CompletedWithErrors => $"Background metadata sync completed with {run.NonSensitiveErrors.Count} isolated errors.",
                _ => $"Background metadata sync: {run.Status}.",
            };
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private Task SearchAsync() => RunAsync(SearchCoreAsync);

    [RelayCommand(CanExecute = nameof(CanCreateSupportBundle))]
    private Task CreateSupportBundleAsync() => RunAsync(
        async cancellationToken =>
        {
            if (supportBundleService is null)
                return;
            LatestSupportBundlePath = await supportBundleService.CreateAsync(cancellationToken);
            SupportBundleStatus =
                "Support bundle created locally. Review the ZIP contents before sharing it.";
            StatusText = "Privacy-safe support bundle created locally.";
        },
        "Creating a privacy-safe support bundle");

    [RelayCommand(CanExecute = nameof(CanCreateSupportBundle))]
    private Task RefreshDiagnosticsAsync() => RunAsync(
        async cancellationToken =>
        {
            if (supportBundleService is null)
                return;
            var events =
                await supportBundleService.ReadRecentAsync(
                    100,
                    cancellationToken);
            _allDiagnosticEvents.Clear();
            _allDiagnosticEvents.AddRange(events);
            ApplyDiagnosticFilter();
        },
        "Loading privacy-safe diagnostics");

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
        var presentationEpoch = ++_sensitivePresentationEpoch;
        using var value = await secretAccessService.RetrieveAsync(
            SelectedResult.Id,
            TimeSpan.FromSeconds(
                SelectedRevealVerificationGrace.Seconds),
            cancellationToken);
        if (presentationEpoch != _sensitivePresentationEpoch || !IsApplicationReady)
            return;
        SecretPreview = value.Reveal();
        _ = HideSecretLaterAsync(value.Mask(), presentationEpoch);
        StatusText =
            $"Secret revealed for 10 seconds. {RevealVerificationGraceStatus}";
    });

    [RelayCommand(CanExecute = nameof(CanCopySelectedSecret))]
    private Task CopyAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await secretAccessService.RetrieveAndCopyAsync(SelectedResult.Id, TimeSpan.FromSeconds(Math.Clamp(ClipboardClearSeconds, 5, 300)), EffectivePolicy(), cancellationToken);
        StatusText = $"Copied. Clipboard clears after {Math.Clamp(ClipboardClearSeconds, 5, 300)} seconds if unchanged.";
    });

    [RelayCommand(CanExecute = nameof(CanCacheSelectedSecret))]
    private Task CacheSelectedAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        await secretAccessService.RetrieveAndCacheAsync(SelectedResult.Id, SelectedWorkspace?.Id, TimeSpan.FromHours(MaximumCacheHours), EffectivePolicy(), cancellationToken);
        StatusText = $"Encrypted offline copy expires in {MaximumCacheHours} hours.";
    });

    [RelayCommand(CanExecute = nameof(CanOpenOfflineSecret))]
    private Task OpenOfflineAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedResult is null) return;
        var presentationEpoch = ++_sensitivePresentationEpoch;
        using var value = await secretAccessService.RetrieveCachedAsync(SelectedResult.Id, cancellationToken);
        if (presentationEpoch != _sensitivePresentationEpoch || !IsApplicationReady)
            return;
        SecretPreview = value.Reveal();
        _ = HideSecretLaterAsync(value.Mask(), presentationEpoch);
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
        var workspace = new Workspace(Guid.NewGuid(), WorkspaceName.Trim(), string.Empty, Workspaces.Count, GlobalPolicy());
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

    [RelayCommand(CanExecute = nameof(CanAddSelectedTenantToWorkspace))]
    private Task AddSelectedTenantToWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null || SelectedTenant is null) return;
        await workspaceService.AddResourceAsync(SelectedWorkspace.Id, ResourceLinkType.Tenant, SelectedTenant.TenantId, cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = $"Added tenant {SelectedTenant.DisplayName} to {SelectedWorkspace.Name}.";
    });

    [RelayCommand(CanExecute = nameof(CanAddSelectedSubscriptionToWorkspace))]
    private Task AddSelectedSubscriptionToWorkspaceAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null || SelectedSubscription is null) return;
        await workspaceService.AddResourceAsync(SelectedWorkspace.Id, ResourceLinkType.Subscription, SelectedSubscription.SubscriptionId, cancellationToken);
        await SearchCoreAsync(cancellationToken);
        StatusText = $"Added subscription {SelectedSubscription.DisplayName} to {SelectedWorkspace.Name}.";
    });

    [RelayCommand(CanExecute = nameof(CanUseSelectedWorkspace))]
    private Task SaveWorkspacePolicyAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedWorkspace is null) return;
        var updated = SelectedWorkspace with
        {
            CachePolicyOverride = EnterprisePolicy().Constrain(
                new CachePolicy(
                    WorkspaceCacheEnabled,
                    TimeSpan.FromHours(Math.Clamp(WorkspaceMaximumCacheHours, 1, 168)),
                    true,
                    WorkspaceAllowClipboard)),
        };
        await workspaceService.SaveAsync(updated, cancellationToken);
        await ReloadWorkspacesAsync(cancellationToken);
        StatusText = $"Saved secure cache policy for {updated.Name}. Local verification remains mandatory.";
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

    [RelayCommand(CanExecute = nameof(CanManageRecoveryArchives))]
    private Task RefreshRecoveryArchivesAsync() => RunAsync(async cancellationToken =>
    {
        await ReloadRecoveryArchivesCoreAsync(cancellationToken);
        StatusText = RecoveryArchives.Count == 0
            ? "No recovery archives are present."
            : $"{RecoveryArchives.Count} recovery archive(s) are retained locally.";
    });

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedRecoveryArchive))]
    private Task DeleteSelectedRecoveryArchiveAsync() => RunAsync(async cancellationToken =>
    {
        if (localRecoveryArchiveService is null ||
            SelectedRecoveryArchive is null)
        {
            return;
        }

        await localRecoveryArchiveService.DeleteAsync(
            SelectedRecoveryArchive.Id,
            RecoveryArchiveDeleteConfirmation,
            cancellationToken);
        SelectedRecoveryArchive = null;
        RecoveryArchiveDeleteConfirmation = string.Empty;
        await ReloadRecoveryArchivesCoreAsync(cancellationToken);
        StatusText =
            "The selected recovery archive was permanently deleted after Windows verification.";
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

    private void ReplaceSyncErrors(SyncRun run)
    {
        SelectedSyncError = null;
        SyncErrors.Clear();
        var details = run.ErrorDetails ??
            run.NonSensitiveErrors
                .Select((message, index) => new SyncErrorDetail(
                    $"Affected scope {index + 1}",
                    "Unavailable",
                    message,
                    "Use the safe category shown here to correct the affected scope, then retry synchronization."))
                .ToArray();
        foreach (var detail in details)
            SyncErrors.Add(new SyncErrorRow(detail));
        OnPropertyChanged(nameof(HasSyncErrors));
    }

    private async Task RefreshSearchFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var selectedTenant = SelectedTenantFilterOption?.Value;
        var selectedSubscription = SelectedSubscriptionFilterOption?.Value;
        var selectedVault = SelectedVaultFilterOption?.Value;
        var tenants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var subscriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var identity in Identities)
        {
            foreach (var tenant in await repository.GetTenantsAsync(identity.Id, cancellationToken))
                tenants.TryAdd(tenant.TenantId, $"{tenant.DisplayName} · {tenant.TenantId}");
            foreach (var subscription in await repository.GetSubscriptionsAsync(identity.Id, cancellationToken))
                subscriptions.TryAdd(
                    subscription.SubscriptionId,
                    $"{subscription.DisplayName} · {subscription.SubscriptionId}");
            foreach (var vault in await repository.GetVaultAccessSummariesAsync(identity.Id, cancellationToken))
                vaults.TryAdd(vault.Vault.Name, vault.Vault.Name);
        }

        ReplaceFilterOptions(TenantFilterOptions, "All tenants", tenants);
        ReplaceFilterOptions(SubscriptionFilterOptions, "All subscriptions", subscriptions);
        ReplaceFilterOptions(VaultFilterOptions, "All vaults", vaults);
        SelectedTenantFilterOption = FindFilterOption(TenantFilterOptions, selectedTenant);
        SelectedSubscriptionFilterOption = FindFilterOption(SubscriptionFilterOptions, selectedSubscription);
        SelectedVaultFilterOption = FindFilterOption(VaultFilterOptions, selectedVault);
    }

    private static void ReplaceFilterOptions(
        ObservableCollection<SearchFilterOption> target,
        string allLabel,
        IReadOnlyDictionary<string, string> options)
    {
        target.Clear();
        target.Add(SearchFilterOption.All(allLabel));
        foreach (var option in options
                     .Select(pair => new SearchFilterOption(pair.Key, pair.Value))
                     .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase))
            target.Add(option);
    }

    private static SearchFilterOption FindFilterOption(
        IEnumerable<SearchFilterOption> options,
        string? value) =>
        options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)) ??
        options.First();

    private async Task ReloadIdentitiesAsync(CancellationToken cancellationToken)
    {
        var selectedIdentityId = SelectedIdentity?.Id;
        _isReloadingIdentities = true;
        try
        {
            Identities.Clear();
            foreach (var identity in await repository.GetIdentitiesAsync(cancellationToken)) Identities.Add(identity);
            SelectedIdentity = selectedIdentityId is null
                ? Identities.FirstOrDefault()
                : Identities.FirstOrDefault(identity => identity.Id == selectedIdentityId) ?? Identities.FirstOrDefault();
            IsFirstRun = Identities.Count == 0;
            if (IsFirstRun)
                SelectedMainTabIndex = 1;
        }
        finally
        {
            _isReloadingIdentities = false;
        }

        if (SelectedIdentity is null)
        {
            Tenants.Clear();
            Subscriptions.Clear();
            VaultAccessPaths.Clear();
            SelectedTenant = null;
            SelectedSubscription = null;
            SelectedVaultAccess = null;
        }
        else
        {
            await ReloadSubscriptionsCoreAsync(SelectedIdentity.Id, cancellationToken);
        }
        OnPropertyChanged(nameof(SetupConnectionsStatus));
        OnPropertyChanged(nameof(SetupScopeStatus));
        ContinueToSearchCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadSubscriptionsCoreAsync(Guid identityId, CancellationToken cancellationToken)
    {
        var tenants = await repository.GetTenantsAsync(identityId, cancellationToken);
        var subscriptions = await repository.GetSubscriptionsAsync(identityId, cancellationToken);
        var vaultAccessPaths = await repository.GetVaultAccessSummariesAsync(identityId, cancellationToken);
        if (SelectedIdentity?.Id != identityId) return;

        var selectedSubscriptionId = SelectedSubscription?.Id;
        var selectedVaultAccessId = SelectedVaultAccess?.Id;
        var selectedTenantId = SelectedTenant?.Id;
        var enterprise = EnterprisePolicy();
        var azureAllowed = enterprise.AllowedProviders.Contains(
            EnterpriseProvider.AzureKeyVault);
        var visibleTenants = azureAllowed
            ? tenants
                .Where(tenant =>
                    !enterprise.RestrictsTenants ||
                    enterprise.AllowedTenantIds.Contains(
                        tenant.TenantId))
                .ToArray()
            : [];
        var visibleTenantIds = visibleTenants
            .Select(tenant => tenant.Id)
            .ToHashSet();
        Tenants.Clear();
        foreach (var tenant in visibleTenants) Tenants.Add(tenant);
        SelectedTenant = selectedTenantId is null
            ? Tenants.FirstOrDefault()
            : Tenants.FirstOrDefault(tenant => tenant.Id == selectedTenantId) ?? Tenants.FirstOrDefault();
        Subscriptions.Clear();
        foreach (var subscription in subscriptions.Where(
                     subscription =>
                         azureAllowed &&
                         (!enterprise.RestrictsTenants ||
                          visibleTenantIds.Contains(
                              subscription.TenantAccessId))))
            Subscriptions.Add(new SubscriptionSelectionRow(subscription));
        SelectedSubscription = selectedSubscriptionId is null
            ? Subscriptions.FirstOrDefault()
            : Subscriptions.FirstOrDefault(subscription => subscription.Id == selectedSubscriptionId) ??
              Subscriptions.FirstOrDefault();
        VaultAccessPaths.Clear();
        foreach (var accessPath in vaultAccessPaths.Where(
                     accessPath =>
                         azureAllowed &&
                         (!enterprise.RestrictsTenants ||
                          enterprise.AllowedTenantIds.Contains(
                              accessPath.Vault.TenantId))))
            VaultAccessPaths.Add(new VaultAccessRow(accessPath));
        SelectedVaultAccess = selectedVaultAccessId is null
            ? VaultAccessPaths.FirstOrDefault()
            : VaultAccessPaths.FirstOrDefault(access => access.Id == selectedVaultAccessId) ??
              VaultAccessPaths.FirstOrDefault();
        OnPropertyChanged(nameof(SetupScopeStatus));
    }

    private async Task ReloadSubscriptionsAfterSelectionAsync(Guid identityId, int loadVersion)
    {
        try
        {
            await ReloadSubscriptionsCoreAsync(identityId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (loadVersion != _subscriptionLoadVersion || SelectedIdentity?.Id != identityId) return;
            var error = UserFacingErrorMapper.From(ex);
            ErrorTitle = error.Title;
            ErrorMessage = error.Message;
            RecoveryText = error.Recovery;
            HasActionableError = true;
            ErrorAnnouncement = $"{error.Title}. {error.Message} {error.Recovery}";
        }
    }

    private async Task ReloadWorkspacesAsync(CancellationToken cancellationToken)
    {
        Workspaces.Clear();
        foreach (var workspace in await workspaceService.GetAllAsync(cancellationToken)) Workspaces.Add(workspace);
        if (SelectedWorkspace is not null) SelectedWorkspace = Workspaces.FirstOrDefault(x => x.Id == SelectedWorkspace.Id);
    }

    private async Task SaveSettingsCoreAsync(CancellationToken cancellationToken)
    {
        await settingsStore.SaveAsync(new AppSettings(
            EffectiveClientId(),
            Math.Clamp(ClipboardClearSeconds, 5, 300),
            OfflineCacheEnabled,
            Math.Clamp(MaximumCacheHours, 1, 168),
            UseCustomClientId,
            SelectedCloseBehavior,
            BackgroundMetadataSyncEnabled,
            MinimizeToNotificationArea,
            SelectedRevealVerificationGrace.Seconds), cancellationToken);
    }

    private async Task ReloadRecoveryArchivesCoreAsync(
        CancellationToken cancellationToken)
    {
        RecoveryArchives.Clear();
        if (localRecoveryArchiveService is null)
        {
            RecoveryArchiveStatus =
                "Recovery archive management is unavailable in this build.";
            return;
        }

        try
        {
            foreach (var archive in await localRecoveryArchiveService
                         .ListAsync(cancellationToken))
            {
                RecoveryArchives.Add(
                    new LocalRecoveryArchiveRow(archive));
            }

            RecoveryArchiveStatus = RecoveryArchives.Count == 0
                ? "No recovery archives are present."
                : "Delete an archive only after deciding that recovery and support evidence are no longer needed.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            RecoveryArchiveStatus =
                "Recovery archives could not be inspected safely. Their contents were not changed.";
        }
    }

    private string EffectiveClientId() => UseCustomClientId ? ClientId.Trim() : ProductIdentity.DefaultClientId;

    private bool CanStartOperation() => !IsBusy;
    private bool CanCreateSupportBundle() =>
        supportBundleService is not null &&
        !IsBusy;
    private bool CanAddIdentity() =>
        !IsBusy &&
        IsProviderAllowed(EnterpriseProvider.AzureKeyVault) &&
        EnterprisePolicy().AllowedIdentityTypes.Contains(
            SelectedIdentityType) &&
        (SelectedIdentityType != IdentityType.ManagedIdentity ||
         _managedIdentityHostSupported);
    private bool CanRemoveIdentity() =>
        CanUseSelectedIdentity() &&
        string.Equals(
            IdentityRemovalConfirmation.Trim(),
            "REMOVE",
            StringComparison.Ordinal);
    private bool CanManageRecoveryArchives() =>
        localRecoveryArchiveService is not null &&
        !IsBusy;
    private bool CanDeleteSelectedRecoveryArchive() =>
        CanManageRecoveryArchives() &&
        SelectedRecoveryArchive is not null &&
        string.Equals(
            RecoveryArchiveDeleteConfirmation.Trim(),
            LocalRecoveryArchiveService.ConfirmationPhrase,
            StringComparison.Ordinal);
    private bool CanUseSelectedIdentity() => SelectedIdentity is not null && !IsBusy;
    private bool CanContinueToSearch() =>
        SelectedIdentity is not null &&
        !IsBusy &&
        (Results.Count > 0 ||
         Subscriptions.Count > 0 ||
         VaultAccessPaths.Count > 0 ||
         SetupSyncStatus is "Complete" or "Complete with isolated errors");
    private bool CanDisableIdentity() => IsSelectedIdentityEnabled && !IsBusy;
    private bool CanEnableIdentity() => IsSelectedIdentityDisabled && !IsBusy;
    private bool CanUseSelectedIdentityOnline() =>
        SelectedIdentity is
        {
            IsEnabled: true,
            AuthenticationState: AuthenticationState.Ready,
        } identity &&
        IsIdentityAllowed(identity) &&
        !IsBusy;
    private bool CanRetrySelectedSyncError() =>
        CanUseSelectedIdentityOnline() &&
        SelectedSyncError?.CanRetry == true;
    private bool CanAdministerWorkloadIdentities() =>
        workloadIdentityAdministrationService is not null &&
        SelectedIdentity is
        {
            Type: IdentityType.InteractiveUser,
            IsEnabled: true,
            AuthenticationState: AuthenticationState.Ready,
        } identity &&
        IsIdentityAllowed(identity) &&
        !IsBusy;
    private bool CanDiscoverManagedIdentities() =>
        CanAdministerWorkloadIdentities() &&
        EnterprisePolicy().AllowedIdentityTypes.Contains(
            IdentityType.ManagedIdentity) &&
        Guid.TryParse(AdministrationSubscriptionId, out _);
    private bool CanDiscoverServicePrincipals() =>
        CanAdministerWorkloadIdentities() &&
        EnterprisePolicy().AllowedIdentityTypes.Contains(
            IdentityType.ServicePrincipal);
    private bool CanAssessWorkloadIdentityPermissions() =>
        CanAdministerWorkloadIdentities() &&
        SelectedWorkloadIdentityCandidate is { } candidate &&
        IsWorkloadCandidateTypeAllowed(candidate.Candidate.IdentityType) &&
        !string.IsNullOrWhiteSpace(AdministrationVaultResourceId);
    private bool CanPreviewManagedIdentity() =>
        CanDiscoverManagedIdentities() &&
        !string.IsNullOrWhiteSpace(AdministrationResourceGroup) &&
        !string.IsNullOrWhiteSpace(AdministrationIdentityName);
    private bool CanPreviewServicePrincipal() =>
        CanDiscoverServicePrincipals() &&
        !string.IsNullOrWhiteSpace(AdministrationIdentityName);
    private bool CanRotateSelectedCredential() =>
        SelectedIdentitySupportsCredentialRotation &&
        !string.IsNullOrWhiteSpace(ReplacementCredentialData) &&
        !IsBusy;
    private bool CanUseSelectedResult() => SelectedResult is not null && !IsBusy;
    private bool CanUseSelectedSecret() => SelectedResult?.Result.Item.ObjectType is VaultObjectType.Secret && !IsBusy;
    private bool CanCopySelectedSecret() => EffectivePolicy().AllowClipboard && CanUseSelectedSecret();
    private bool CanCacheSelectedSecret() => EffectivePolicy().IsEnabled && CanUseSelectedSecret();
    private bool CanOpenOfflineSecret() =>
        EnterprisePolicy().AllowOfflineCache &&
        CanUseSelectedSecret();
    private bool CanUseSelectedWorkspace() => SelectedWorkspace is not null && !IsBusy;
    private bool CanCreateWorkspace() => !string.IsNullOrWhiteSpace(WorkspaceName) && !IsBusy;
    private bool CanAddSelectedVaultToWorkspace() => SelectedWorkspace is not null && SelectedResult is not null && !IsBusy;
    private bool CanAddSelectedIdentityToWorkspace() => SelectedWorkspace is not null && SelectedIdentity is not null && !IsBusy;
    private bool CanAddSelectedTenantToWorkspace() => SelectedWorkspace is not null && SelectedTenant is not null && !IsBusy;
    private bool CanAddSelectedSubscriptionToWorkspace() => SelectedWorkspace is not null && SelectedSubscription is not null && !IsBusy;
    private bool CanExcludeSubscription() => SelectedSubscription is { IsSelected: true } && !IsBusy;
    private bool CanIncludeSubscription() => SelectedSubscription is { IsSelected: false } && !IsBusy;
    private bool CanExcludeVault() => SelectedVaultAccess is { IsSelected: true } && !IsBusy;
    private bool CanIncludeVault() => SelectedVaultAccess is { IsSelected: false } && !IsBusy;

    partial void OnSelectedIdentityChanged(ConnectedIdentity? value)
    {
        revealVerificationSession?.Invalidate();
        CancelPendingGovernedMutation();
        OnPropertyChanged(nameof(HasSelectedIdentity));
        OnPropertyChanged(nameof(IsSelectedIdentityEnabled));
        OnPropertyChanged(nameof(IsSelectedIdentityDisabled));
        OnPropertyChanged(nameof(ActiveIdentityContext));
        OnPropertyChanged(nameof(BrowserSelectedSource));
        OnPropertyChanged(nameof(SelectedIdentitySupportsCredentialRotation));
        OnPropertyChanged(nameof(CredentialRotationLabel));
        if (value is null) FilterSelectedIdentity = false;
        ReplacementCredentialData = string.Empty;
        IdentityRemovalConfirmation = string.Empty;
        RemoveIdentityCommand.NotifyCanExecuteChanged();
        PurgeSelectedIdentityCacheCommand.NotifyCanExecuteChanged();
        SynchronizeCommand.NotifyCanExecuteChanged();
        RetrySelectedSyncErrorCommand.NotifyCanExecuteChanged();
        PrepareGovernedMutationCommand.NotifyCanExecuteChanged();
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();
        ReauthenticateIdentityCommand.NotifyCanExecuteChanged();
        DisableIdentityCommand.NotifyCanExecuteChanged();
        EnableIdentityCommand.NotifyCanExecuteChanged();
        RevokeIdentityAccessCommand.NotifyCanExecuteChanged();
        RotateSelectedCredentialCommand.NotifyCanExecuteChanged();
        AuthorizeDirectoryReadCommand.NotifyCanExecuteChanged();
        DiscoverManagedIdentitiesCommand.NotifyCanExecuteChanged();
        DiscoverServicePrincipalsCommand.NotifyCanExecuteChanged();
        AssessWorkloadIdentityPermissionsCommand.NotifyCanExecuteChanged();
        NotifyCyberArkCommands();
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();
        PreviewServicePrincipalCommand.NotifyCanExecuteChanged();
        RefreshSubscriptionsCommand.NotifyCanExecuteChanged();
        ContinueToSearchCommand.NotifyCanExecuteChanged();
        AddSelectedIdentityToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedTenantToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedSubscriptionToWorkspaceCommand.NotifyCanExecuteChanged();
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();
        ReplaceWorkloadCandidates([]);
        Tenants.Clear();
        Subscriptions.Clear();
        VaultAccessPaths.Clear();
        SelectedTenant = null;
        SelectedSubscription = null;
        SelectedVaultAccess = null;
        var loadVersion = ++_subscriptionLoadVersion;
        if (!_isReloadingIdentities && IsApplicationReady && value is not null)
            _ = ReloadSubscriptionsAfterSelectionAsync(value.Id, loadVersion);
    }

    partial void OnIdentityRemovalConfirmationChanged(string value) =>
        RemoveIdentityCommand.NotifyCanExecuteChanged();

    partial void OnSelectedSubscriptionChanged(SubscriptionSelectionRow? value)
    {
        OnPropertyChanged(nameof(ActiveSubscriptionContext));
        ExcludeSubscriptionCommand.NotifyCanExecuteChanged();
        IncludeSubscriptionCommand.NotifyCanExecuteChanged();
        AddSelectedSubscriptionToWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSyncErrorChanged(SyncErrorRow? value) =>
        RetrySelectedSyncErrorCommand.NotifyCanExecuteChanged();

    partial void OnSelectedTenantChanged(TenantAccess? value) =>
        AddSelectedTenantToWorkspaceCommand.NotifyCanExecuteChanged();

    partial void OnSelectedVaultAccessChanged(VaultAccessRow? value)
    {
        CancelPendingGovernedMutation();
        ExcludeVaultCommand.NotifyCanExecuteChanged();
        IncludeVaultCommand.NotifyCanExecuteChanged();
        PrepareGovernedMutationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedResultChanged(SearchResultRow? value)
    {
        OnPropertyChanged(nameof(BrowserSelectedSource));
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        RevealCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CacheSelectedCommand.NotifyCanExecuteChanged();
        OpenOfflineCommand.NotifyCanExecuteChanged();
        PurgeSelectedCachedValueCommand.NotifyCanExecuteChanged();
        PurgeSelectedVaultCacheCommand.NotifyCanExecuteChanged();
        AddSelectedVaultToWorkspaceCommand.NotifyCanExecuteChanged();
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedWorkspaceChanged(Workspace? value)
    {
        revealVerificationSession?.Invalidate();
        OnPropertyChanged(nameof(HasSelectedWorkspace));
        OnPropertyChanged(nameof(ActiveWorkspaceContext));
        if (value is null) FilterSelectedWorkspace = false;
        PurgeSelectedWorkspaceCacheCommand.NotifyCanExecuteChanged();
        AddSelectedVaultToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedIdentityToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedTenantToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedSubscriptionToWorkspaceCommand.NotifyCanExecuteChanged();
        RemoveWorkspaceCommand.NotifyCanExecuteChanged();
        SaveWorkspacePolicyCommand.NotifyCanExecuteChanged();
        if (value is not null)
        {
            var policy = EnterprisePolicy().Constrain(
                value.CachePolicyOverride ?? CachePolicy.SecureDefault);
            WorkspaceCacheEnabled = policy.IsEnabled;
            WorkspaceMaximumCacheHours = Math.Clamp((int)policy.MaximumLifetime.TotalHours, 1, 168);
            WorkspaceAllowClipboard = policy.AllowClipboard;
        }
        CopyCommand.NotifyCanExecuteChanged();
        CacheSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTenantFilterOptionChanged(SearchFilterOption? value) =>
        TenantFilter = value?.Value ?? string.Empty;

    partial void OnSelectedSubscriptionFilterOptionChanged(SearchFilterOption? value) =>
        SubscriptionFilter = value?.Value ?? string.Empty;

    partial void OnSelectedVaultFilterOptionChanged(SearchFilterOption? value) =>
        VaultFilter = value?.Value ?? string.Empty;

    partial void OnOfflineCacheEnabledChanged(bool value)
    {
        CacheSelectedCommand.NotifyCanExecuteChanged();
        OpenOfflineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRevealVerificationGraceChanged(
        RevealVerificationGraceOption value)
    {
        revealVerificationSession?.Invalidate();
        OnPropertyChanged(nameof(RevealVerificationGraceStatus));
    }

    partial void OnWorkspaceNameChanged(string value)
    {
        CreateWorkspaceCommand.NotifyCanExecuteChanged();
    }

    partial void OnWorkloadIdentitySearchTextChanged(
        string value) =>
        ApplyWorkloadIdentityFilter();

    partial void OnDiagnosticSearchTextChanged(
        string value) =>
        ApplyDiagnosticFilter();

    partial void OnIsBusyChanged(bool value)
    {
        AddIdentityCommand.NotifyCanExecuteChanged();
        RemoveIdentityCommand.NotifyCanExecuteChanged();
        ReauthenticateIdentityCommand.NotifyCanExecuteChanged();
        DisableIdentityCommand.NotifyCanExecuteChanged();
        EnableIdentityCommand.NotifyCanExecuteChanged();
        RevokeIdentityAccessCommand.NotifyCanExecuteChanged();
        RotateSelectedCredentialCommand.NotifyCanExecuteChanged();
        AuthorizeDirectoryReadCommand.NotifyCanExecuteChanged();
        DiscoverManagedIdentitiesCommand.NotifyCanExecuteChanged();
        DiscoverServicePrincipalsCommand.NotifyCanExecuteChanged();
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();
        PreviewServicePrincipalCommand.NotifyCanExecuteChanged();
        SynchronizeCommand.NotifyCanExecuteChanged();
        RetrySelectedSyncErrorCommand.NotifyCanExecuteChanged();
        RefreshSubscriptionsCommand.NotifyCanExecuteChanged();
        ContinueToSearchCommand.NotifyCanExecuteChanged();
        ExcludeSubscriptionCommand.NotifyCanExecuteChanged();
        IncludeSubscriptionCommand.NotifyCanExecuteChanged();
        ExcludeVaultCommand.NotifyCanExecuteChanged();
        IncludeVaultCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        CreateSupportBundleCommand.NotifyCanExecuteChanged();
        RefreshDiagnosticsCommand.NotifyCanExecuteChanged();
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
        AddSelectedTenantToWorkspaceCommand.NotifyCanExecuteChanged();
        AddSelectedSubscriptionToWorkspaceCommand.NotifyCanExecuteChanged();
        RemoveWorkspaceCommand.NotifyCanExecuteChanged();
        SaveWorkspacePolicyCommand.NotifyCanExecuteChanged();
        SaveSettingsCommand.NotifyCanExecuteChanged();
        RefreshRecoveryArchivesCommand.NotifyCanExecuteChanged();
        DeleteSelectedRecoveryArchiveCommand.NotifyCanExecuteChanged();
        AssessWorkloadIdentityPermissionsCommand.NotifyCanExecuteChanged();
        PrepareGovernedMutationCommand.NotifyCanExecuteChanged();
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRecoveryArchiveChanged(
        LocalRecoveryArchiveRow? value)
    {
        RecoveryArchiveDeleteConfirmation = string.Empty;
        DeleteSelectedRecoveryArchiveCommand.NotifyCanExecuteChanged();
    }

    partial void OnRecoveryArchiveDeleteConfirmationChanged(
        string value) =>
        DeleteSelectedRecoveryArchiveCommand.NotifyCanExecuteChanged();

    partial void OnReplacementCredentialDataChanged(string value) =>
        RotateSelectedCredentialCommand.NotifyCanExecuteChanged();

    partial void OnAdministrationSubscriptionIdChanged(string value)
    {
        DiscoverManagedIdentitiesCommand.NotifyCanExecuteChanged();
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();
    }

    partial void OnAdministrationResourceGroupChanged(string value) =>
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();

    partial void OnAdministrationVaultResourceIdChanged(string value) =>
        AssessWorkloadIdentityPermissionsCommand.NotifyCanExecuteChanged();

    partial void OnSelectedWorkloadIdentityCandidateChanged(
        WorkloadIdentityCandidateRow? value) =>
        AssessWorkloadIdentityPermissionsCommand.NotifyCanExecuteChanged();

    partial void OnAdministrationIdentityNameChanged(string value)
    {
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();
        PreviewServicePrincipalCommand.NotifyCanExecuteChanged();
    }

    private CachePolicy GlobalPolicy() => new(OfflineCacheEnabled, TimeSpan.FromHours(Math.Clamp(MaximumCacheHours, 1, 168)), true, true);
    private CachePolicy EffectivePolicy() => EnterprisePolicy().Constrain(
        SelectedWorkspace?.CachePolicyOverride ?? GlobalPolicy());

    private EnterprisePolicySnapshot EnterprisePolicy() =>
        (enterprisePolicy ?? UnmanagedEnterprisePolicy.Instance)
            .GetSnapshot();

    private bool IsProviderAllowed(EnterpriseProvider provider) =>
        EnterprisePolicy().AllowedProviders.Contains(provider);

    private bool IsIdentityAllowed(ConnectedIdentity identity)
    {
        try
        {
            EnterprisePolicy().EnsureIdentityAllowed(identity);
            return true;
        }
        catch (EnterprisePolicyDeniedException)
        {
            return false;
        }
    }

    private bool IsWorkloadCandidateTypeAllowed(string candidateType)
    {
        var allowedIdentityTypes = EnterprisePolicy().AllowedIdentityTypes;
        return candidateType switch
        {
            "User-assigned managed identity" =>
                allowedIdentityTypes.Contains(
                    IdentityType.ManagedIdentity),
            "Service principal" =>
                allowedIdentityTypes.Contains(
                    IdentityType.ServicePrincipal),
            _ => false,
        };
    }

    private void ApplyEnterprisePolicyToPreferences()
    {
        var policy = EnterprisePolicy();
        EnterprisePolicyStatus = policy.SafeStatus;
        OnPropertyChanged(nameof(IsEnterpriseOfflineCacheAllowed));
        OnPropertyChanged(nameof(IsEnterpriseClipboardAllowed));
        OnPropertyChanged(nameof(RevealVerificationGraceStatus));
        OfflineCacheEnabled &= policy.AllowOfflineCache;
        if (policy.MaximumOfflineCacheLifetime is { } maximum)
        {
            MaximumCacheHours = Math.Min(
                MaximumCacheHours,
                Math.Max(1, (int)Math.Floor(maximum.TotalHours)));
        }

        var availableIdentityTypes = SupportedIdentityTypes
            .Where(identityType =>
                policy.AllowedIdentityTypes.Contains(identityType))
            .ToArray();

        if (!availableIdentityTypes.Contains(SelectedIdentityType) &&
            availableIdentityTypes.Length > 0)
        {
            SelectedIdentityType = availableIdentityTypes[0];
        }

        SynchronizeIdentityTypes(availableIdentityTypes);

        AddIdentityCommand.NotifyCanExecuteChanged();
        AuthorizeDirectoryReadCommand.NotifyCanExecuteChanged();
        DiscoverManagedIdentitiesCommand.NotifyCanExecuteChanged();
        DiscoverServicePrincipalsCommand.NotifyCanExecuteChanged();
        AssessWorkloadIdentityPermissionsCommand.NotifyCanExecuteChanged();
        PreviewManagedIdentityCommand.NotifyCanExecuteChanged();
        PreviewServicePrincipalCommand.NotifyCanExecuteChanged();
        SynchronizeCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CacheSelectedCommand.NotifyCanExecuteChanged();
        OpenOfflineCommand.NotifyCanExecuteChanged();
        NotifyCyberArkCommands();
    }

    private void SynchronizeIdentityTypes(
        IdentityType[] availableIdentityTypes)
    {
        // Preserve a valid selection while policy and host availability are refreshed.
        // Clearing the bound collection makes Avalonia push null into the enum property.
        for (var index = 0; index < availableIdentityTypes.Length; index++)
        {
            var identityType = availableIdentityTypes[index];
            if (index < IdentityTypes.Count &&
                IdentityTypes[index] == identityType)
            {
                continue;
            }

            var existingIndex = IdentityTypes.IndexOf(identityType);
            if (existingIndex >= 0)
            {
                IdentityTypes.Move(existingIndex, index);
            }
            else
            {
                IdentityTypes.Insert(index, identityType);
            }
        }

        while (IdentityTypes.Count > availableIdentityTypes.Length)
        {
            IdentityTypes.RemoveAt(IdentityTypes.Count - 1);
        }
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ReplaceWorkloadCandidates(IReadOnlyList<WorkloadIdentityCandidate> candidates)
    {
        SelectedWorkloadIdentityCandidate = null;
        WorkloadIdentityCandidates.Clear();
        _allWorkloadIdentityCandidates.Clear();
        _allWorkloadIdentityCandidates.AddRange(
            candidates);
        ApplyWorkloadIdentityFilter();
    }

    private void ApplyWorkloadIdentityFilter(
        string? selectPrincipalId = null)
    {
        var search = WorkloadIdentitySearchText.Trim();
        var source =
            _allWorkloadIdentityCandidates.Count > 0
                ? _allWorkloadIdentityCandidates
                : WorkloadIdentityCandidates
                    .Select(row => row.Candidate)
                    .ToList();
        var filtered = source
            .Where(candidate =>
                search.Length == 0 ||
                CandidateContains(
                    candidate,
                    search))
            .OrderBy(
                candidate => candidate.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                candidate => candidate.ClientId,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectedWorkloadIdentityCandidate = null;
        WorkloadIdentityCandidates.Clear();
        foreach (var candidate in filtered)
        {
            WorkloadIdentityCandidates.Add(
                new WorkloadIdentityCandidateRow(
                    candidate));
        }

        if (!string.IsNullOrWhiteSpace(selectPrincipalId))
        {
            SelectedWorkloadIdentityCandidate =
                WorkloadIdentityCandidates.FirstOrDefault(
                    row =>
                        string.Equals(
                            row.Candidate.PrincipalId,
                            selectPrincipalId,
                            StringComparison.OrdinalIgnoreCase));
        }

        WorkloadIdentityFilterStatus =
            $"{filtered.Length} of {source.Count} eligible identities shown. Microsoft first-party infrastructure is excluded.";
    }

    private static bool CandidateContains(
        WorkloadIdentityCandidate candidate,
        string search) =>
        candidate.DisplayName.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        candidate.IdentityType.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        candidate.ClientId.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        candidate.PrincipalId.Contains(
            search,
            StringComparison.OrdinalIgnoreCase);

    private void ApplyDiagnosticFilter()
    {
        var search = DiagnosticSearchText.Trim();
        var filtered = _allDiagnosticEvents
            .Where(diagnosticEvent =>
                search.Length == 0 ||
                DiagnosticContains(
                    diagnosticEvent,
                    search))
            .ToArray();
        DiagnosticEvents.Clear();
        foreach (var diagnosticEvent in filtered)
        {
            DiagnosticEvents.Add(
                new DiagnosticEventRow(
                    diagnosticEvent));
        }

        DiagnosticViewerStatus =
            _allDiagnosticEvents.Count == 0
                ? "No privacy-safe diagnostic events are available."
                : $"Showing {filtered.Length} of {_allDiagnosticEvents.Count} recent privacy-safe diagnostic events, newest first.";
    }

    private static bool DiagnosticContains(
        DiagnosticEvent diagnosticEvent,
        string search) =>
        diagnosticEvent.Level.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        diagnosticEvent.Category.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        diagnosticEvent.Scope.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        diagnosticEvent.Summary.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) ||
        diagnosticEvent.Recovery.Contains(
            search,
            StringComparison.OrdinalIgnoreCase);

    private static string FormatPlan(WorkloadIdentityProvisioningPlan plan)
    {
        var location = string.IsNullOrWhiteSpace(plan.SubscriptionId)
            ? $"Tenant: {plan.TenantId}; subscription/resource group: not applicable unless an exact vault role is included."
            : $"Tenant: {plan.TenantId}; subscription: {plan.SubscriptionId}; resource group: {plan.ResourceGroup}.";
        var operations = plan.Operations.Select(
            (operation, index) =>
                $"{index + 1}. {operation.Operation} {operation.ResourceType}\nScope: {operation.Scope}\nEffect: {operation.ExpectedEffect}");
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                $"PREVIEW ONLY — performs mutations: {plan.PerformsMutations}",
                $"Identity: {plan.IdentityType} / {plan.IdentityName}",
                location,
            }.Concat(operations));
    }

    private static string GetVersion()
    {
        var value = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(value) ? "development" : value.Split('+', 2)[0];
    }

    private async Task HideSecretLaterAsync(string mask, int presentationEpoch)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (presentationEpoch == _sensitivePresentationEpoch)
            SecretPreview = mask;
    }

    private void ConfigureUnlockFailure(UserVerificationResult result)
    {
        (UnlockMessage, UnlockRecovery, StatusText) = result switch
        {
            UserVerificationResult.Canceled => (
                "Vault Prospector remains locked because verification was canceled.",
                "Choose Unlock Vault Prospector when you are ready and complete the Windows prompt.",
                "Application locked — verification canceled."),
            UserVerificationResult.NotConfigured => (
                "Vault Prospector remains locked because Windows Hello is not configured.",
                "Configure Windows Hello for this Windows account, then choose Unlock Vault Prospector.",
                "Application locked — Windows Hello not configured."),
            UserVerificationResult.DisabledByPolicy => (
                "Vault Prospector remains locked because Windows verification is disabled by policy.",
                "Ask your administrator to enable an approved Windows verification method.",
                "Application locked — verification disabled by policy."),
            UserVerificationResult.Unavailable => (
                "Vault Prospector remains locked because the Windows verification device or service is unavailable.",
                "Restore the interactive Windows session or verification device, then retry.",
                "Application locked — verification unavailable."),
            UserVerificationResult.RemoteSessionUnavailable => (
                "Vault Prospector remains locked because Windows verification is unavailable in this Remote Desktop session.",
                "Your administrator may enable current-account Windows credential verification for supported remote sessions. Otherwise reconnect at the Windows or Hyper-V console.",
                "Application locked — Windows verification unavailable in Remote Desktop."),
            UserVerificationResult.RemoteCredentialUnavailable => (
                "Vault Prospector remains locked because the Windows credential prompt is unavailable in this remote session.",
                "Retry from an interactive AVD or Remote Desktop desktop. If the prompt remains unavailable, ask your administrator to review remote verification policy.",
                "Application locked — remote Windows verification unavailable."),
            UserVerificationResult.RemoteCredentialFailed => (
                "Vault Prospector remains locked because the supplied Windows credential did not verify the current signed-in account.",
                "Retry with the credential for this Windows session. Credentials for a different account cannot unlock this user's encrypted data.",
                "Application locked — current Windows account not verified."),
            _ => (
                "Vault Prospector remains locked because verification did not complete.",
                "Retry Windows verification. If attempts are exhausted, follow your Windows Hello recovery process.",
                "Application locked — verification failed."),
        };
    }

    private async Task ConfigureManagedIdentityAvailabilityAsync(CancellationToken cancellationToken)
    {
        ManagedIdentityEnvironmentStatus status;
        try
        {
            status = await managedIdentityEnvironmentDetector.DetectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            status = new ManagedIdentityEnvironmentStatus(
                false,
                "Managed identity availability could not be verified. Use interactive Microsoft Entra sign-in or a certificate-based service principal.");
        }

        ManagedIdentityAvailabilityText = status.SafeReason;
        _managedIdentityHostSupported = status.IsSupported;
        OnPropertyChanged(nameof(IsManagedIdentityHostSupported));
        ApplyEnterprisePolicyToPreferences();
    }

    private static bool IsProtectedLocalDataFailure(Exception exception) =>
        exception is ProtectedKeyUnavailableException
            or IncompatibleLocalDataVersionException
            or LocalDataIntegrityException
            or System.Security.Cryptography.CryptographicException;

    private void ConfigureLocalDataFailure(Exception exception)
    {
        IsLocalDataBlocked = true;
        IsApplicationReady = false;
        IsRecoveryComplete = false;
        ResetConfirmation = string.Empty;
        CanResetLocalData = exception is not IncompatibleLocalDataVersionException;

        (LocalDataFailureTitle, LocalDataRecoveryGuidance, StatusText) = exception switch
        {
            IncompatibleLocalDataVersionException => (
                "Newer Vault Prospector version required",
                "Install the same or a newer Vault Prospector version than the one that last opened this data. The current installation did not modify it.",
                "Local data preserved — newer application required."),
            ProtectedKeyUnavailableException => (
                "Protected local-data key unavailable",
                "Restore the matched data and key set under the same Windows account. If no recovery copy exists, type RESET below to archive the current encrypted state and start fresh after a restart.",
                "Local data preserved — matching protected key required."),
            _ => (
                "Encrypted local data failed validation",
                "Keep the current encrypted state for recovery or support. If you deliberately choose to start fresh, type RESET below; Vault Prospector will archive the entire local state after fresh Windows verification and require a restart.",
                "Local data preserved — recovery decision required."),
        };
    }

    private async Task RunAsync(
        Func<CancellationToken, Task> action,
        string operationText = "the active operation")
    {
        if (IsBusy) return;
        ApplyEnterprisePolicyToPreferences();
        ClearActionableError();
        ActiveOperationText = operationText;
        IsBusy = true;
        using var operation = new CancellationTokenSource();
        _activeOperation = operation;
        try { await action(operation.Token); }
        catch (OperationCanceledException)
        {
            if (IsUnlocked)
                StatusText = "Operation cancelled.";
            SecretPreview = "Secret hidden.";
            HideCyberArkValue();
        }
        catch (Exception ex)
        {
            var error = UserFacingErrorMapper.From(ex);
            ErrorTitle = error.Title;
            ErrorMessage = error.Message;
            RecoveryText = error.Recovery;
            HasActionableError = true;
            ErrorAnnouncement = $"{error.Title}. {error.Message} {error.Recovery}";
            SecretPreview = "Secret hidden.";
            HideCyberArkValue();
        }
        finally
        {
            _activeOperation = null;
            IsBusy = false;
            ActiveOperationText = string.Empty;
        }
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

public sealed class LocalRecoveryArchiveRow(
    LocalRecoveryArchive archive)
{
    public LocalRecoveryArchive Archive { get; } = archive;
    public string Id => Archive.Id;
    public string Kind => Archive.Kind switch
    {
        LocalRecoveryArchiveKind.Reset => "Reset recovery",
        LocalRecoveryArchiveKind.Rotation => "Pre-rotation recovery",
        LocalRecoveryArchiveKind.FailedRotation =>
            "Interrupted rotation evidence",
        _ => "Recovery",
    };
    public string Created =>
        Archive.CreatedAtUtc
            .ToLocalTime()
            .ToString(
                "g",
                System.Globalization.CultureInfo.CurrentCulture);
    public string Size => FormatSize(Archive.SizeBytes);
    public string Summary =>
        $"{Kind} · {Created} · {Size}";

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.0} KiB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024):0.0} MiB";
        return $"{bytes / (1024d * 1024 * 1024):0.0} GiB";
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
    public string AccessStatus => Result.AccessStatus;
    public bool IsFavorite => Result.IsFavorite;
    public string FavoriteMarker => IsFavorite ? "★" : "☆";
}

public sealed record SearchFilterOption(string Value, string Label)
{
    public static SearchFilterOption All(string label) => new(string.Empty, label);
}

public sealed class SyncErrorRow(SyncErrorDetail detail)
{
    public SyncErrorDetail Detail { get; } = detail;
    public string Scope { get; } = detail.Scope;
    public string Category { get; } = detail.Category;
    public string Message { get; } = detail.Message;
    public string Recovery { get; } = detail.Recovery;
    public string Timestamp { get; } = detail.OccurredAt is null
        ? "Time unavailable"
        : detail.OccurredAt.Value.ToLocalTime().ToString(
            "yyyy-MM-dd HH:mm:ss zzz",
            System.Globalization.CultureInfo.InvariantCulture);
    public string CorrelationId { get; } = string.IsNullOrWhiteSpace(detail.CorrelationId)
        ? "Unavailable"
        : detail.CorrelationId;
    public bool CanRetry { get; } = detail.RetryScope is not null &&
        (!string.IsNullOrWhiteSpace(detail.RetryScope.SubscriptionId) ||
         !string.IsNullOrWhiteSpace(detail.RetryScope.VaultResourceId));
}

public sealed class DiagnosticEventRow(
    DiagnosticEvent diagnosticEvent)
{
    public string Timestamp { get; } =
        diagnosticEvent.Timestamp.ToLocalTime()
            .ToString(
                "yyyy-MM-dd HH:mm:ss zzz",
                System.Globalization.CultureInfo
                    .InvariantCulture);
    public string Level { get; } =
        diagnosticEvent.Level;
    public string Category { get; } =
        diagnosticEvent.Category;
    public string Scope { get; } =
        diagnosticEvent.Scope;
    public string Summary { get; } =
        diagnosticEvent.Summary;
    public string Recovery { get; } =
        diagnosticEvent.Recovery;
}

public sealed class SubscriptionSelectionRow(SubscriptionAccess subscription)
{
    public SubscriptionAccess Subscription { get; } = subscription;
    public Guid Id => Subscription.Id;
    public string SubscriptionId => Subscription.SubscriptionId;
    public string DisplayName => Subscription.DisplayName;
    public string State => Subscription.State;
    public bool IsSelected => Subscription.IsSelected;
    public string InclusionState => IsSelected ? "Included" : "Excluded";
}

public sealed class VaultAccessRow(VaultAccessSummary summary)
{
    public VaultAccessSummary Summary { get; } = summary;
    public Guid Id => Summary.Access.Id;
    public Guid VaultId => Summary.Vault.Id;
    public string Vault => Summary.Vault.Name;
    public string Subscription => Summary.Vault.SubscriptionId;
    public string Tenant => Summary.TenantDisplayName;
    public string Identity => Summary.IdentityDisplayName;
    public string PermissionSummary => Summary.Access.AccessStatus;
    public string FailureCategory => Summary.Access.LastFailureCategory ?? "None";
    public bool IsSelected => Summary.Access.IsSelected;
    public string InclusionState => IsSelected ? "Included" : "Excluded";
}

public sealed class WorkloadIdentityCandidateRow(WorkloadIdentityCandidate candidate)
{
    public WorkloadIdentityCandidate Candidate { get; } = candidate;
    public string DisplayName => Candidate.DisplayName;
    public string IdentityType => Candidate.IdentityType;
    public string ClientId => Candidate.ClientId;
    public string PrincipalId => Candidate.PrincipalId;
    public string Scope => string.IsNullOrWhiteSpace(Candidate.ResourceId)
        ? Candidate.TenantId
        : Candidate.ResourceId;
    public string State => Candidate.IsEnabled ? "Enabled" : "Disabled";
    public string PermissionSummary =>
        $"View: {Candidate.Permissions.DirectoryVisibility}\n" +
        $"Attach/use: {Candidate.Permissions.AttachOrUse}\n" +
        $"Manage identity: {Candidate.Permissions.IdentityManagement}\n" +
        $"Key Vault data: {Candidate.Permissions.KeyVaultDataAccess}\n" +
        $"Role assignments: {Candidate.Permissions.RoleAssignmentManagement}\n" +
        (Candidate.Permissions.Evidence.Count == 0
            ? "Detailed evidence: not assessed for an exact Key Vault."
            : "Detailed evidence:\n" +
              string.Join(
                  "\n",
                  Candidate.Permissions.Evidence.Select(evidence =>
                      $"{evidence.Capability} — {evidence.State}\n" +
                      $"Subject: {evidence.Subject}\n" +
                      $"Scope: {evidence.Scope}\n" +
                      $"Basis: {evidence.Basis}\n" +
                      $"Observed: {evidence.ObservedAt:yyyy-MM-dd HH:mm:ss} UTC")));
}
