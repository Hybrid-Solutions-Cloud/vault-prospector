using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
    private BrowserFillApproval? _pendingBrowserFillApproval;
    private TaskCompletionSource<BrowserFillResponse>? _pendingBrowserFillCompletion;
    private CancellationTokenSource? _pendingBrowserFillCancellation;
    private Guid? _capturedBrowserMappingId;
    private bool _capturedBrowserPolicyAllowed;

    public ObservableCollection<BrowserFillMappingRow> BrowserFillMappings { get; } = [];
    public ObservableCollection<BrowserFillAuditRow> BrowserFillAudit { get; } = [];
    public ObservableCollection<BrowserSourceOption> BrowserEligibleSources { get; } = [];
    public IReadOnlyList<BrowserMappingFieldPurpose> BrowserFieldPurposes { get; } =
        [
            BrowserMappingFieldPurpose.Username,
            BrowserMappingFieldPurpose.Password,
            BrowserMappingFieldPurpose.OneTimeCode,
        ];

    public event EventHandler? BrowserFillConfirmationRequested;

    [ObservableProperty] private BrowserFillMappingRow? _selectedBrowserFillMapping;
    [ObservableProperty] private BrowserSourceOption? _selectedBrowserSourceOption;
    [ObservableProperty] private string _browserTopOrigin = string.Empty;
    [ObservableProperty] private string _browserFrameOrigin = string.Empty;
    [ObservableProperty]
    private BrowserMappingFieldPurpose _selectedBrowserFieldPurpose =
        BrowserMappingFieldPurpose.Password;
    [ObservableProperty] private bool _browserMappingEnabled = true;
    [ObservableProperty] private bool _isBrowserFillPending;
    [ObservableProperty] private bool _isBrowserFillProcessing;
    [ObservableProperty]
    private string _browserFillStatus =
        "No browser fill request is waiting. Values are never offered without an exact mapping.";
    [ObservableProperty]
    private string _browserPolicyStatus =
        "Browser fill is disabled until machine policy is loaded.";
    [ObservableProperty] private string _browserFillDestination = string.Empty;
    [ObservableProperty] private string _browserFillSource = string.Empty;
    [ObservableProperty] private string _browserFillPurpose = string.Empty;
    [ObservableProperty] private bool _hasCapturedBrowserDestination;
    [ObservableProperty]
    private string _browserCapturedDestination =
        "No destination captured. Focus a supported sign-in field and choose the Vault Prospector browser extension.";
    [ObservableProperty]
    private string _browserExtensionStatus =
        "Not yet confirmed. Invoke the extension from a supported HTTPS sign-in field.";
    [ObservableProperty]
    private string _browserNativeHostStatus =
        "Not yet confirmed. A successful destination capture proves the native host path.";
    [ObservableProperty]
    private string _browserBrokerStatus =
        "Desktop broker status has not been reported.";
    [ObservableProperty]
    private string _browserDestinationPolicyStatus =
        "Capture a destination to evaluate its exact machine policy.";
    [ObservableProperty]
    private string _browserMappingReadinessStatus =
        "Capture a destination to create or review its exact mapping.";

    public bool BrowserIntegrationAvailable => browserFillService is not null;
    public string BrowserSelectedSource =>
        SelectedResult is null || SelectedIdentity is null
            ? "Select one secret and its exact identity on Search and Identities first."
            : $"{SelectedResult.Name} from {SelectedResult.Vault} through {SelectedIdentity.DisplayName}";

    public void SetBrowserBrokerAvailability(bool isAvailable)
    {
        BrowserBrokerStatus = isAvailable
            ? "Ready. The current-user desktop broker is listening for the trusted native host."
            : "Unavailable. Restart Vault Prospector; if this persists, collect a support bundle.";
    }

    public async Task<BrowserFillResponse> RequestBrowserFillAsync(
        ValidatedBrowserFillRequest request,
        CancellationToken cancellationToken)
    {
        if (browserFillService is null ||
            !IsUnlocked ||
            !IsApplicationReady ||
            IsCloseChoiceVisible ||
            _pendingBrowserFillApproval is not null)
        {
            return BrowserFillResponse.Failure(
                request.Request.RequestId,
                BrowserFillResultCode.Unavailable);
        }

        var assessment = await browserFillService.AssessDestinationAsync(
            request,
            cancellationToken);
        await CaptureBrowserDestinationAsync(
            assessment,
            cancellationToken);
        SelectedMainTabIndex = 4;
        BrowserFillConfirmationRequested?.Invoke(this, EventArgs.Empty);

        var approval = await browserFillService.PrepareAsync(
            request,
            cancellationToken);
        if (approval is null)
        {
            BrowserFillStatus = !assessment.PolicyDecision.IsAllowed
                ? "Destination captured, but machine policy denies it. Ask an administrator to allow this exact browser, top frame, target frame, and field purpose."
                : assessment.ExistingMapping is null
                    ? "Destination captured. Select one eligible secret and identity below, review the destination, then create the mapping. Invoke the extension again to fill."
                    : "Destination captured, but its mapping is disabled or its source is unavailable. Review the mapping and selected source before trying again.";
            return BrowserFillResponse.Failure(
                request.Request.RequestId,
                BrowserFillResultCode.Denied);
        }

        _pendingBrowserFillApproval = approval;
        _pendingBrowserFillCompletion =
            new TaskCompletionSource<BrowserFillResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingBrowserFillCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var remaining = approval.ExpiresAt - DateTimeOffset.UtcNow;
        _pendingBrowserFillCancellation.CancelAfter(
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        BrowserFillDestination = approval.Request.TopOrigin.SerializedOrigin ==
                                 approval.Request.FrameOrigin.SerializedOrigin
            ? approval.Request.TopOrigin.SerializedOrigin
            : $"{approval.Request.FrameOrigin.SerializedOrigin} inside {approval.Request.TopOrigin.SerializedOrigin}";
        BrowserFillSource =
            $"{approval.ItemName} · {approval.VaultName} · {approval.IdentityDisplayName}";
        BrowserFillPurpose = approval.Mapping.FieldPurpose.ToString();
        BrowserFillStatus =
            "Review the exact source and destination. Approve requires a fresh Windows verification.";
        IsBrowserFillPending = true;
        ApproveBrowserFillCommand.NotifyCanExecuteChanged();
        DenyBrowserFillCommand.NotifyCanExecuteChanged();

        using var cancellationRegistration = _pendingBrowserFillCancellation.Token.Register(
            () => _pendingBrowserFillCompletion.TrySetResult(
                BrowserFillResponse.Failure(
                    request.Request.RequestId,
                    BrowserFillResultCode.Expired)));
        try
        {
            return await _pendingBrowserFillCompletion.Task;
        }
        finally
        {
            if (_pendingBrowserFillApproval?.ApprovalId == approval.ApprovalId)
                ClearPendingBrowserFill();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveBrowserMapping))]
    private Task SaveBrowserMappingAsync() => RunAsync(async cancellationToken =>
    {
        if (browserFillService is null ||
            SelectedResult is null ||
            SelectedIdentity is null)
        {
            return;
        }

        var mapping = await browserFillService.SaveMappingAsync(
            _capturedBrowserMappingId,
            SelectedResult.Id,
            SelectedIdentity.Id,
            BrowserTopOrigin,
            BrowserFrameOrigin,
            SelectedBrowserFieldPurpose,
            BrowserMappingEnabled,
            cancellationToken);
        await ReloadBrowserIntegrationAsync(cancellationToken);
        SelectedBrowserFillMapping =
            BrowserFillMappings.FirstOrDefault(row => row.Id == mapping.Id);
        _capturedBrowserMappingId = mapping.Id;
        BrowserMappingReadinessStatus =
            "Ready. An enabled local mapping now binds this exact destination, field purpose, secret, and identity.";
        StatusText =
            "Browser mapping saved. Return to the focused browser field and invoke the extension again; no value was retrieved during setup.";
    });

    [RelayCommand(CanExecute = nameof(CanRemoveBrowserMapping))]
    private Task RemoveBrowserMappingAsync() => RunAsync(async cancellationToken =>
    {
        if (browserFillService is null || SelectedBrowserFillMapping is null)
            return;
        await browserFillService.RemoveMappingAsync(
            SelectedBrowserFillMapping.Id,
            cancellationToken);
        SelectedBrowserFillMapping = null;
        await ReloadBrowserIntegrationAsync(cancellationToken);
        StatusText = "Browser mapping removed. Existing audit history was retained.";
    });

    [RelayCommand(CanExecute = nameof(CanRefreshBrowserIntegration))]
    private Task RefreshBrowserIntegrationAsync() => RunAsync(async cancellationToken =>
    {
        await ReloadBrowserIntegrationAsync(cancellationToken);
        StatusText =
            $"{BrowserFillMappings.Count} browser mapping(s) loaded. No values were retrieved.";
    });

    [RelayCommand(CanExecute = nameof(CanApproveBrowserFill))]
    private async Task ApproveBrowserFillAsync()
    {
        if (browserFillService is null ||
            _pendingBrowserFillApproval is null ||
            _pendingBrowserFillCompletion is null ||
            _pendingBrowserFillCancellation is null)
        {
            return;
        }

        IsBrowserFillProcessing = true;
        ApproveBrowserFillCommand.NotifyCanExecuteChanged();
        DenyBrowserFillCommand.NotifyCanExecuteChanged();
        var approval = _pendingBrowserFillApproval;
        var completion = _pendingBrowserFillCompletion;
        var operationToken = _pendingBrowserFillCancellation.Token;
        try
        {
            var response = await browserFillService.ApproveAsync(
                approval,
                operationToken);
            BrowserFillStatus = response.Result == BrowserFillResultCode.Approved
                ? "Approved value returned for this one request. The extension must recheck the page before filling."
                : "The request was denied because its mapped context changed or expired.";
            if (!completion.TrySetResult(response) &&
                response.ValueUtf8 is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(
                    response.ValueUtf8);
            }
            await ReloadBrowserIntegrationAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            BrowserFillStatus =
                "The request was denied. Verify the mapping, identity, Windows verification, and Azure access before trying again.";
            completion.TrySetResult(
                BrowserFillResponse.Failure(
                    approval.Request.Request.RequestId,
                    BrowserFillResultCode.Denied));
        }
        finally
        {
            IsBrowserFillProcessing = false;
            ApproveBrowserFillCommand.NotifyCanExecuteChanged();
            DenyBrowserFillCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDenyBrowserFill))]
    private async Task DenyBrowserFillAsync()
    {
        if (browserFillService is null ||
            _pendingBrowserFillApproval is null ||
            _pendingBrowserFillCompletion is null)
        {
            return;
        }

        var approval = _pendingBrowserFillApproval;
        IsBrowserFillProcessing = true;
        try
        {
            await browserFillService.DenyAsync(approval, CancellationToken.None);
            BrowserFillStatus = "Browser fill denied. No value was retrieved.";
            _pendingBrowserFillCompletion.TrySetResult(
                BrowserFillResponse.Failure(
                    approval.Request.Request.RequestId,
                    BrowserFillResultCode.Denied));
            await ReloadBrowserIntegrationAsync(CancellationToken.None);
        }
        finally
        {
            IsBrowserFillProcessing = false;
            ApproveBrowserFillCommand.NotifyCanExecuteChanged();
            DenyBrowserFillCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ReloadBrowserIntegrationAsync(CancellationToken cancellationToken)
    {
        if (browserFillService is null)
            return;

        if (browserIntegrationDiagnostics is not null)
        {
            var setup = await browserIntegrationDiagnostics.InspectAsync(
                cancellationToken);
            BrowserExtensionStatus = setup.ExtensionStatus;
            BrowserNativeHostStatus = setup.NativeHostStatus;
        }

        BrowserPolicyStatus =
            await browserFillService.GetPolicyStatusAsync(cancellationToken);
        var selectedId = SelectedBrowserFillMapping?.Id;
        BrowserFillMappings.Clear();
        foreach (var mapping in await browserFillService.GetMappingsAsync(cancellationToken))
            BrowserFillMappings.Add(new BrowserFillMappingRow(mapping));
        SelectedBrowserFillMapping = selectedId is null
            ? null
            : BrowserFillMappings.FirstOrDefault(row => row.Id == selectedId);

        BrowserFillAudit.Clear();
        foreach (var audit in await browserFillService.GetAuditAsync(50, cancellationToken))
            BrowserFillAudit.Add(new BrowserFillAuditRow(audit));
    }

    private async Task CaptureBrowserDestinationAsync(
        BrowserDestinationAssessment assessment,
        CancellationToken cancellationToken)
    {
        BrowserTopOrigin =
            assessment.Request.TopOrigin.SerializedOrigin;
        BrowserFrameOrigin =
            assessment.Request.FrameOrigin.SerializedOrigin;
        SelectedBrowserFieldPurpose = assessment.FieldPurpose;
        BrowserMappingEnabled = true;
        HasCapturedBrowserDestination = true;
        BrowserCapturedDestination =
            BrowserTopOrigin == BrowserFrameOrigin
                ? $"{BrowserTopOrigin} · {assessment.FieldPurpose}"
                : $"{BrowserFrameOrigin} inside {BrowserTopOrigin} · {assessment.FieldPurpose}";
        BrowserExtensionStatus =
            $"Connected from {assessment.Request.Request.BrowserFamily}. The extension supplied the active tab, frame, and focused-field purpose.";
        BrowserNativeHostStatus =
            "Connected. The browser extension reached the trusted native host and current-user desktop broker.";
        _capturedBrowserPolicyAllowed =
            assessment.PolicyDecision.IsAllowed;
        BrowserDestinationPolicyStatus =
            assessment.PolicyDecision.IsAllowed
                ? $"Allowed. {assessment.PolicyDecision.SafeReason}"
                : $"Denied. {assessment.PolicyDecision.SafeReason}";
        _capturedBrowserMappingId =
            assessment.ExistingMapping?.Id;
        BrowserMappingReadinessStatus =
            assessment.ExistingMapping is null
                ? "No local mapping exists. Select an eligible secret and identity, then create this exact mapping."
                : assessment.ExistingMapping.IsEnabled
                    ? "An enabled mapping exists. Review its source before requesting a one-time fill."
                    : "A mapping exists but is disabled. Review its source before enabling it.";
        await ReloadBrowserEligibleSourcesAsync(cancellationToken);
        if (assessment.ExistingMapping is { } existingMapping)
        {
            SelectedBrowserSourceOption =
                BrowserEligibleSources.FirstOrDefault(option =>
                    option.Result.Id == existingMapping.VaultItemId &&
                    option.Identity.Id == existingMapping.ConnectedIdentityId);
        }
        await ReloadBrowserIntegrationAsync(cancellationToken);
        SelectedBrowserFillMapping =
            _capturedBrowserMappingId is { } mappingId
                ? BrowserFillMappings.FirstOrDefault(
                    row => row.Id == mappingId)
                : null;
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadBrowserEligibleSourcesAsync(
        CancellationToken cancellationToken)
    {
        var previousItemId =
            SelectedBrowserSourceOption?.Result.Id;
        var previousIdentityId =
            SelectedBrowserSourceOption?.Identity.Id;
        BrowserEligibleSources.Clear();
        var discoveredSources = new HashSet<(Guid ItemId, Guid IdentityId)>();
        var results = await searchService.SearchAsync(
            new SearchRequest(
                ObjectType: VaultObjectType.Secret,
                Enabled: true,
                Limit: 1000),
            cancellationToken);
        foreach (var result in results)
        {
            foreach (var identity in Identities.Where(identity =>
                         identity.IsEnabled &&
                         identity.AuthenticationState ==
                         AuthenticationState.Ready &&
                         IsIdentityAllowed(identity)))
            {
                var resolved =
                    await repository.ResolveItemForIdentityAsync(
                        result.Item.Id,
                        identity.Id,
                        cancellationToken);
                if (resolved is null)
                    continue;
                if (!discoveredSources.Add((result.Item.Id, identity.Id)))
                    continue;
                BrowserEligibleSources.Add(
                    new BrowserSourceOption(
                        new SearchResultRow(result),
                        identity));
            }
        }

        SelectedBrowserSourceOption =
            BrowserEligibleSources.FirstOrDefault(option =>
                option.Result.Id == previousItemId &&
                option.Identity.Id == previousIdentityId);
    }

    private void ClearBrowserDestinationCapture()
    {
        _capturedBrowserMappingId = null;
        _capturedBrowserPolicyAllowed = false;
        HasCapturedBrowserDestination = false;
        SelectedBrowserSourceOption = null;
        BrowserEligibleSources.Clear();
        BrowserTopOrigin = string.Empty;
        BrowserFrameOrigin = string.Empty;
        BrowserCapturedDestination =
            "No destination captured. Focus a supported sign-in field and choose the Vault Prospector browser extension.";
        BrowserDestinationPolicyStatus =
            "Capture a destination to evaluate its exact machine policy.";
        BrowserMappingReadinessStatus =
            "Capture a destination to create or review its exact mapping.";
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();
    }

    private void CancelPendingBrowserFill(string status)
    {
        var approval = _pendingBrowserFillApproval;
        var completion = _pendingBrowserFillCompletion;
        if (approval is null || completion is null)
            return;

        BrowserFillStatus = status;
        _pendingBrowserFillCancellation?.Cancel();
        completion.TrySetResult(
            BrowserFillResponse.Failure(
                approval.Request.Request.RequestId,
                BrowserFillResultCode.Denied));
        if (browserFillService is not null)
            _ = RecordBrowserDenialSafelyAsync(browserFillService, approval);
    }

    private static async Task RecordBrowserDenialSafelyAsync(
        BrowserFillService service,
        BrowserFillApproval approval)
    {
        try
        {
            await service.DenyAsync(approval, CancellationToken.None);
        }
        catch (Exception)
        {
            // The security boundary remains denied even when value-free audit persistence fails.
        }
    }

    private void ClearPendingBrowserFill()
    {
        var cancellation = _pendingBrowserFillCancellation;
        _pendingBrowserFillApproval = null;
        _pendingBrowserFillCompletion = null;
        _pendingBrowserFillCancellation = null;
        IsBrowserFillPending = false;
        IsBrowserFillProcessing = false;
        BrowserFillDestination = string.Empty;
        BrowserFillSource = string.Empty;
        BrowserFillPurpose = string.Empty;
        ApproveBrowserFillCommand.NotifyCanExecuteChanged();
        DenyBrowserFillCommand.NotifyCanExecuteChanged();
        cancellation?.Dispose();
    }

    private bool CanSaveBrowserMapping() =>
        browserFillService is not null &&
        SelectedResult?.Result.Item.ObjectType == VaultObjectType.Secret &&
        SelectedIdentity is not null &&
        HasCapturedBrowserDestination &&
        _capturedBrowserPolicyAllowed &&
        !IsBusy;

    private bool CanRemoveBrowserMapping() =>
        browserFillService is not null &&
        SelectedBrowserFillMapping is not null &&
        !IsBusy;

    private bool CanRefreshBrowserIntegration() =>
        browserFillService is not null && !IsBusy;

    private bool CanApproveBrowserFill() =>
        IsBrowserFillPending &&
        !IsBrowserFillProcessing &&
        _pendingBrowserFillApproval is not null;

    private bool CanDenyBrowserFill() => CanApproveBrowserFill();

    partial void OnBrowserTopOriginChanged(string value) =>
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();

    partial void OnBrowserFrameOriginChanged(string value) =>
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();

    partial void OnSelectedBrowserFillMappingChanged(BrowserFillMappingRow? value)
    {
        RemoveBrowserMappingCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedBrowserFieldPurposeChanged(
        BrowserMappingFieldPurpose value) =>
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();

    partial void OnSelectedBrowserSourceOptionChanged(
        BrowserSourceOption? value)
    {
        SelectedResult = value?.Result;
        SelectedIdentity = value?.Identity;
        SaveBrowserMappingCommand.NotifyCanExecuteChanged();
    }
}

public sealed class BrowserSourceOption(
    SearchResultRow result,
    ConnectedIdentity identity)
{
    public SearchResultRow Result { get; } = result;
    public ConnectedIdentity Identity { get; } = identity;
    public string Summary =>
        $"{Result.Name} · {Result.Vault} · {Identity.DisplayName}";
}

public sealed class BrowserFillMappingRow(BrowserFillMapping mapping)
{
    public BrowserFillMapping Mapping { get; } = mapping;
    public Guid Id => Mapping.Id;
    public string Destination => Mapping.TopOrigin == Mapping.FrameOrigin
        ? Mapping.TopOrigin
        : $"{Mapping.FrameOrigin} inside {Mapping.TopOrigin}";
    public string Purpose => Mapping.FieldPurpose.ToString();
    public string State => Mapping.IsEnabled ? "Enabled" : "Disabled";
    public string Summary => $"{Purpose} · {Destination} · {State}";
}

public sealed class BrowserFillAuditRow(BrowserFillAuditEvent auditEvent)
{
    public BrowserFillAuditEvent AuditEvent { get; } = auditEvent;
    public string Occurred =>
        AuditEvent.OccurredAt.ToLocalTime().ToString(
            "g",
            System.Globalization.CultureInfo.CurrentCulture);
    public string Summary =>
        $"{Occurred} · {AuditEvent.Result} · {AuditEvent.FieldPurpose} · {AuditEvent.FrameOrigin}";
}
