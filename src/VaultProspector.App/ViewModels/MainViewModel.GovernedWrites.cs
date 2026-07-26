using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Domain;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
    public IReadOnlyList<GovernedAzureOperation>
        GovernedAzureOperations { get; } =
        Enum.GetValues<GovernedAzureOperation>();

    public bool IsGovernedAzureMutationAvailable =>
        governedAzureMutationService?.IsAvailable == true;

    [ObservableProperty]
    private GovernedAzureOperation _selectedGovernedAzureOperation =
        GovernedAzureOperation.CreateSecret;
    [ObservableProperty]
    private string _governedMutationObjectName = string.Empty;
    [ObservableProperty]
    private string _governedMutationSensitiveValue = string.Empty;
    [ObservableProperty]
    private string _governedMutationConfirmation = string.Empty;
    [ObservableProperty]
    private string _governedMutationPreviewText =
        "No governed Azure mutation preview is active.";
    [ObservableProperty]
    private string _governedMutationStatus =
        "Read-only mode. A valid machine policy and accepted release gate are required.";
    [ObservableProperty]
    private GovernedMutationPreview? _pendingGovernedMutationPreview;

    public bool GovernedMutationRequiresSensitiveValue =>
        SelectedGovernedAzureOperation is
            GovernedAzureOperation.CreateSecret or
            GovernedAzureOperation.CreateSecretVersion;
    public bool HasPendingGovernedMutationPreview =>
        PendingGovernedMutationPreview is not null;

    [RelayCommand(CanExecute = nameof(CanPrepareGovernedMutation))]
    private Task PrepareGovernedMutationAsync() => RunAsync(
        async cancellationToken =>
        {
            if (governedAzureMutationService is null ||
                SelectedIdentity is null ||
                SelectedVaultAccess is null)
            {
                return;
            }
            if (SelectedVaultAccess.Summary.Access.ConnectedIdentityId !=
                SelectedIdentity.Id)
            {
                throw new InvalidOperationException(
                    "Select the identity that owns the selected Key Vault access path.");
            }

            CancelPendingGovernedMutation();
            var preview = await governedAzureMutationService.PrepareAsync(
                SelectedIdentity,
                SelectedVaultAccess.Summary.Vault,
                SelectedGovernedAzureOperation,
                GovernedMutationObjectName,
                cancellationToken);
            PendingGovernedMutationPreview = preview;
            GovernedMutationConfirmation = string.Empty;
            GovernedMutationSensitiveValue = string.Empty;
            GovernedMutationPreviewText = FormatGovernedMutationPreview(
                preview);
            GovernedMutationStatus =
                "ELEVATED OPERATION PREPARED. Review every value-free field, enter any required secret value, and type the one-time confirmation phrase.";
        },
        "Preparing a governed Azure mutation preview");

    [RelayCommand(CanExecute = nameof(CanExecuteGovernedMutation))]
    private Task ExecuteGovernedMutationAsync() => RunAsync(
        async cancellationToken =>
        {
            if (governedAzureMutationService is null ||
                PendingGovernedMutationPreview is null)
            {
                return;
            }

            var preview = PendingGovernedMutationPreview;
            SensitiveValue? sensitiveValue = null;
            try
            {
                if (preview.Operation is
                    GovernedAzureOperation.CreateSecret or
                    GovernedAzureOperation.CreateSecretVersion)
                {
                    sensitiveValue = new SensitiveValue(
                        GovernedMutationSensitiveValue);
                }
                var result = await governedAzureMutationService.ExecuteAsync(
                    preview,
                    GovernedMutationConfirmation,
                    sensitiveValue,
                    cancellationToken);
                sensitiveValue = null;
                GovernedMutationStatus =
                    $"{result.SafeStatus} Provider version/operation: {result.ProviderVersion}. {result.RecoveryGuidance}";
                StatusText = result.SafeStatus;
            }
            finally
            {
                sensitiveValue?.Dispose();
                GovernedMutationSensitiveValue = string.Empty;
                GovernedMutationConfirmation = string.Empty;
                PendingGovernedMutationPreview = null;
                GovernedMutationPreviewText =
                    "No governed Azure mutation preview is active.";
            }
        },
        "Executing one governed Azure mutation");

    [RelayCommand]
    private void CancelGovernedMutation()
    {
        CancelPendingGovernedMutation();
        GovernedMutationStatus =
            "Governed mutation canceled. No Azure mutation was submitted.";
    }

    private void CancelPendingGovernedMutation()
    {
        if (PendingGovernedMutationPreview is { } preview)
            governedAzureMutationService?.Cancel(preview.Id);
        PendingGovernedMutationPreview = null;
        GovernedMutationSensitiveValue = string.Empty;
        GovernedMutationConfirmation = string.Empty;
        GovernedMutationPreviewText =
            "No governed Azure mutation preview is active.";
    }

    private bool CanPrepareGovernedMutation() =>
        governedAzureMutationService?.IsAvailable == true &&
        SelectedIdentity is
        {
            IsEnabled: true,
            AuthenticationState: AuthenticationState.Ready,
        } &&
        SelectedVaultAccess is not null &&
        !string.IsNullOrWhiteSpace(GovernedMutationObjectName) &&
        !IsBusy;

    private bool CanExecuteGovernedMutation() =>
        governedAzureMutationService?.IsAvailable == true &&
        PendingGovernedMutationPreview is not null &&
        !string.IsNullOrWhiteSpace(GovernedMutationConfirmation) &&
        (!GovernedMutationRequiresSensitiveValue ||
         !string.IsNullOrEmpty(GovernedMutationSensitiveValue)) &&
        !IsBusy;

    partial void OnSelectedGovernedAzureOperationChanged(
        GovernedAzureOperation value)
    {
        CancelPendingGovernedMutation();
        OnPropertyChanged(
            nameof(GovernedMutationRequiresSensitiveValue));
        PrepareGovernedMutationCommand.NotifyCanExecuteChanged();
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();
    }

    partial void OnGovernedMutationObjectNameChanged(string value)
    {
        CancelPendingGovernedMutation();
        PrepareGovernedMutationCommand.NotifyCanExecuteChanged();
    }

    partial void OnGovernedMutationSensitiveValueChanged(string value) =>
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();

    partial void OnGovernedMutationConfirmationChanged(string value) =>
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();

    partial void OnPendingGovernedMutationPreviewChanged(
        GovernedMutationPreview? value)
    {
        OnPropertyChanged(
            nameof(HasPendingGovernedMutationPreview));
        ExecuteGovernedMutationCommand.NotifyCanExecuteChanged();
    }

    private static string FormatGovernedMutationPreview(
        GovernedMutationPreview preview) =>
        $"""
        ELEVATED — ONE OPERATION ONLY

        Operation: {preview.Operation}
        Identity: {preview.IdentityDisplayName} ({preview.IdentityId:D})
        Tenant: {preview.TenantId}
        Subscription: {preview.SubscriptionId}
        Resource group: {preview.ResourceGroup}
        Vault: {preview.VaultName}
        Vault resource ID: {preview.VaultResourceId}
        Object: {preview.ObjectName}
        Expected effect: {preview.ExpectedEffect}
        Expected current version: {(string.IsNullOrWhiteSpace(preview.ExpectedCurrentVersion) ? "Not applicable" : preview.ExpectedCurrentVersion)}
        Preview expires: {preview.ExpiresAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}
        Confirmation phrase: {preview.ConfirmationPhrase}
        Recovery: {preview.RecoveryGuidance}

        No secret, key material, certificate material, or private key is included in this preview.
        """;
}
