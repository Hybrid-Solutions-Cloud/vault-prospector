using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
    public ObservableCollection<CyberArkProfile> CyberArkProfiles { get; } = [];
    public ObservableCollection<CyberArkSafeRow> CyberArkSafes { get; } = [];
    public ObservableCollection<CyberArkAccountRow> CyberArkAccounts { get; } = [];
    public ObservableCollection<CyberArkVersionRow> CyberArkVersions { get; } = [];
    public ObservableCollection<CyberArkAuditRow> CyberArkAudit { get; } = [];

    [ObservableProperty] private CyberArkProfile? _selectedCyberArkProfile;
    [ObservableProperty] private CyberArkAccountRow? _selectedCyberArkAccount;
    [ObservableProperty] private CyberArkVersionRow? _selectedCyberArkVersion;
    [ObservableProperty] private string _cyberArkProfileName = string.Empty;
    [ObservableProperty] private string _cyberArkIdentityUrl = string.Empty;
    [ObservableProperty] private string _cyberArkPrivilegeCloudUrl = string.Empty;
    [ObservableProperty] private string _cyberArkServiceUserName = string.Empty;
    [ObservableProperty] private string _cyberArkApplicationName = string.Empty;
    [ObservableProperty] private string _cyberArkClientCredential = string.Empty;
    [ObservableProperty] private string _cyberArkSearchText = string.Empty;
    [ObservableProperty] private string _cyberArkRetrievalReason = string.Empty;
    [ObservableProperty] private string _cyberArkValuePreview = "CyberArk value hidden.";
    [ObservableProperty]
    private string _cyberArkPermissionSummary =
        "Select an account to inspect direct safe-member permission evidence.";
    [ObservableProperty]
    private string _cyberArkStatus =
        "CyberArk Privilege Cloud is configured separately from Azure.";
    [ObservableProperty] private string _cyberArkRemoveConfirmation = string.Empty;

    [RelayCommand(CanExecute = nameof(CanConnectCyberArk))]
    private Task ConnectCyberArkAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null)
            throw new InvalidOperationException(
                "CyberArk integration is unavailable in this build.");
        if (!Uri.TryCreate(
                CyberArkIdentityUrl.Trim(),
                UriKind.Absolute,
                out var identityUrl) ||
            !Uri.TryCreate(
                CyberArkPrivilegeCloudUrl.Trim(),
                UriKind.Absolute,
                out var privilegeCloudUrl))
        {
            throw new CyberArkConfigurationException(
                "Both CyberArk endpoints must be absolute HTTPS URLs.");
        }

        var profile = new CyberArkProfile(
            SelectedCyberArkProfile?.Id ?? Guid.NewGuid(),
            CyberArkProfileName.Trim(),
            identityUrl,
            privilegeCloudUrl,
            CyberArkServiceUserName.Trim(),
            CyberArkApplicationName.Trim(),
            CyberArkAuthenticationState.Unknown,
            true,
            DateTimeOffset.MinValue,
            null);
        using var credential = new SensitiveValue(CyberArkClientCredential);
        CyberArkClientCredential = string.Empty;
        var connected = await cyberArkService.ConnectAsync(
            profile,
            credential,
            cancellationToken);
        await ReloadCyberArkProfilesAsync(cancellationToken);
        SelectedCyberArkProfile = CyberArkProfiles.First(
            item => item.Id == connected.Id);
        CyberArkStatus =
            "CyberArk profile validated. The credential is protected for this Windows account and is not stored in metadata.";
        StatusText = $"Connected CyberArk profile {connected.DisplayName}.";
    });

    [RelayCommand(CanExecute = nameof(CanManageCyberArk))]
    private Task SynchronizeCyberArkAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        var snapshot = await cyberArkService.SynchronizeAsync(
            SelectedCyberArkProfile.Id,
            cancellationToken);
        await ReloadCyberArkSourceAsync(cancellationToken);
        CyberArkStatus =
            $"Metadata sync completed: {snapshot.Safes.Count} safes, {snapshot.Accounts.Count} accounts, {snapshot.Versions.Count} versions, {snapshot.Errors.Count} isolated errors. No values were retrieved.";
        StatusText = "CyberArk metadata synchronization completed.";
    });

    [RelayCommand(CanExecute = nameof(CanUseCyberArkProfile))]
    private Task SearchCyberArkAsync() => RunAsync(async cancellationToken =>
    {
        await ReloadCyberArkAccountsAsync(cancellationToken);
        CyberArkStatus =
            $"{CyberArkAccounts.Count} CyberArk account(s) matched. Search used encrypted local metadata only.";
    });

    [RelayCommand(CanExecute = nameof(CanUseCyberArkAccount))]
    private Task RevealCyberArkAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkAccount is null)
            return;
        var presentationEpoch = ++_sensitivePresentationEpoch;
        using var value = await cyberArkService.RetrieveAsync(
            SelectedCyberArkAccount.Account,
            SelectedCyberArkVersion?.Version.VersionId,
            CyberArkRetrievalReason,
            "show",
            cancellationToken);
        if (presentationEpoch != _sensitivePresentationEpoch ||
            !IsApplicationReady)
            return;
        CyberArkValuePreview = value.Reveal();
        _ = HideCyberArkValueLaterAsync(value.Mask(), presentationEpoch);
        CyberArkStatus =
            "CyberArk value revealed for 10 seconds after fresh Windows verification.";
    });

    [RelayCommand(CanExecute = nameof(CanUseCyberArkAccount))]
    private Task CopyCyberArkAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkAccount is null)
            return;
        await cyberArkService.CopyAsync(
            SelectedCyberArkAccount.Account,
            SelectedCyberArkVersion?.Version.VersionId,
            CyberArkRetrievalReason,
            TimeSpan.FromSeconds(Math.Clamp(ClipboardClearSeconds, 5, 300)),
            cancellationToken);
        CyberArkStatus =
            $"CyberArk value copied after fresh Windows verification. Clipboard clears after {Math.Clamp(ClipboardClearSeconds, 5, 300)} seconds if unchanged.";
    });

    [RelayCommand(CanExecute = nameof(CanUseCyberArkProfile))]
    private Task ToggleCyberArkProfileAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        var enabled = !SelectedCyberArkProfile.IsEnabled;
        await cyberArkService.SetEnabledAsync(
            SelectedCyberArkProfile.Id,
            enabled,
            cancellationToken);
        var id = SelectedCyberArkProfile.Id;
        await ReloadCyberArkProfilesAsync(cancellationToken);
        SelectedCyberArkProfile = CyberArkProfiles.FirstOrDefault(
            profile => profile.Id == id);
        CyberArkStatus = enabled
            ? "CyberArk profile enabled. Validate or synchronize before retrieval."
            : "CyberArk profile disabled. Metadata remains local; retrieval is blocked.";
    });

    [RelayCommand(CanExecute = nameof(CanRevokeCyberArkProfile))]
    private Task RevokeCyberArkProfileAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        var id = SelectedCyberArkProfile.Id;
        await cyberArkService.RevokeLocalAccessAsync(id, cancellationToken);
        CyberArkRemoveConfirmation = string.Empty;
        await ReloadCyberArkProfilesAsync(cancellationToken);
        SelectedCyberArkProfile = CyberArkProfiles.FirstOrDefault(
            profile => profile.Id == id);
        CyberArkStatus =
            "Local CyberArk access was revoked: retrieval is blocked and the protected credential was removed. Revoke the service user in CyberArk Identity to complete external revocation.";
    });

    [RelayCommand(CanExecute = nameof(CanRemoveCyberArkProfile))]
    private Task RemoveCyberArkProfileAsync() => RunAsync(async cancellationToken =>
    {
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        await cyberArkService.RemoveAsync(
            SelectedCyberArkProfile.Id,
            cancellationToken);
        SelectedCyberArkProfile = null;
        CyberArkRemoveConfirmation = string.Empty;
        ClearCyberArkEditor();
        await ReloadCyberArkProfilesAsync(cancellationToken);
        CyberArkStatus =
            "CyberArk profile, protected credential, and synchronized metadata were removed. Value-free audit history was retained.";
    });

    [RelayCommand(CanExecute = nameof(CanUseCyberArkProfile))]
    private void NewCyberArkProfile()
    {
        SelectedCyberArkProfile = null;
        ClearCyberArkEditor();
        CyberArkStatus =
            "Enter a CyberArk Identity endpoint, Privilege Cloud endpoint, service user, application name, and client credential.";
    }

    private async Task ReloadCyberArkProfilesAsync(
        CancellationToken cancellationToken)
    {
        CyberArkProfiles.Clear();
        CyberArkSafes.Clear();
        CyberArkAccounts.Clear();
        CyberArkVersions.Clear();
        CyberArkAudit.Clear();
        if (cyberArkService is null)
        {
            CyberArkStatus =
                "CyberArk integration is unavailable in this build.";
            return;
        }

        var selectedId = SelectedCyberArkProfile?.Id;
        foreach (var profile in await cyberArkService.GetProfilesAsync(
                     cancellationToken))
            CyberArkProfiles.Add(profile);
        SelectedCyberArkProfile = selectedId is null
            ? CyberArkProfiles.FirstOrDefault()
            : CyberArkProfiles.FirstOrDefault(profile => profile.Id == selectedId) ??
              CyberArkProfiles.FirstOrDefault();
    }

    private async Task ReloadCyberArkSourceAsync(
        CancellationToken cancellationToken)
    {
        CyberArkSafes.Clear();
        CyberArkAccounts.Clear();
        CyberArkVersions.Clear();
        CyberArkAudit.Clear();
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        foreach (var safe in await cyberArkService.GetSafesAsync(
                     SelectedCyberArkProfile.Id,
                     cancellationToken))
            CyberArkSafes.Add(new CyberArkSafeRow(safe));
        await ReloadCyberArkAccountsAsync(cancellationToken);
        foreach (var audit in await cyberArkService.GetAuditAsync(
                     SelectedCyberArkProfile.Id,
                     100,
                     cancellationToken))
            CyberArkAudit.Add(new CyberArkAuditRow(audit));
    }

    private async Task ReloadCyberArkAccountsAsync(
        CancellationToken cancellationToken)
    {
        CyberArkAccounts.Clear();
        CyberArkVersions.Clear();
        if (cyberArkService is null || SelectedCyberArkProfile is null)
            return;
        foreach (var account in await cyberArkService.SearchAccountsAsync(
                     SelectedCyberArkProfile.Id,
                     CyberArkSearchText,
                     250,
                     cancellationToken))
            CyberArkAccounts.Add(new CyberArkAccountRow(account));
        SelectedCyberArkAccount = CyberArkAccounts.FirstOrDefault();
    }

    private async Task ReloadCyberArkAccountDetailsAsync(
        CyberArkAccount account)
    {
        try
        {
            CyberArkVersions.Clear();
            if (cyberArkService is null)
                return;
            foreach (var version in await cyberArkService.GetVersionsAsync(
                         account,
                         CancellationToken.None))
                CyberArkVersions.Add(new CyberArkVersionRow(version));
            SelectedCyberArkVersion = CyberArkVersions.FirstOrDefault();
            var safe = CyberArkSafes.FirstOrDefault(
                item => string.Equals(
                    item.Safe.Name,
                    account.SafeName,
                    StringComparison.OrdinalIgnoreCase));
            var permission = safe is null
                ? null
                : await cyberArkService.GetPermissionAsync(
                    account.ProfileId,
                    safe.Safe.SafeId,
                    CancellationToken.None);
            CyberArkPermissionSummary = permission is null
                ? "Direct safe-member permission evidence is unavailable. CyberArk remains authoritative and may include group or role grants."
                : $"Direct member evidence: list={permission.ListAccounts}, use={permission.UseAccounts}, retrieve={permission.RetrieveAccounts}, audit={permission.ViewAuditLog}, confirmation-bypass={permission.AccessWithoutConfirmation}.";
        }
        catch
        {
            CyberArkVersions.Clear();
            CyberArkPermissionSummary =
                "CyberArk account details could not be loaded from encrypted local metadata.";
        }
    }

    private void ClearCyberArkEditor()
    {
        CyberArkProfileName = string.Empty;
        CyberArkIdentityUrl = string.Empty;
        CyberArkPrivilegeCloudUrl = string.Empty;
        CyberArkServiceUserName = string.Empty;
        CyberArkApplicationName = string.Empty;
        CyberArkClientCredential = string.Empty;
        CyberArkSearchText = string.Empty;
        CyberArkRetrievalReason = string.Empty;
        CyberArkSafes.Clear();
        CyberArkAccounts.Clear();
        CyberArkVersions.Clear();
        CyberArkAudit.Clear();
        HideCyberArkValue();
    }

    private void HideCyberArkValue() =>
        CyberArkValuePreview = "CyberArk value hidden.";

    private async Task HideCyberArkValueLaterAsync(
        string masked,
        int presentationEpoch)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (presentationEpoch == _sensitivePresentationEpoch)
            CyberArkValuePreview = masked;
    }

    private bool CanConnectCyberArk() =>
        cyberArkService is not null &&
        !IsBusy &&
        IsApplicationReady &&
        !string.IsNullOrWhiteSpace(CyberArkProfileName) &&
        !string.IsNullOrWhiteSpace(CyberArkIdentityUrl) &&
        !string.IsNullOrWhiteSpace(CyberArkPrivilegeCloudUrl) &&
        !string.IsNullOrWhiteSpace(CyberArkServiceUserName) &&
        !string.IsNullOrWhiteSpace(CyberArkApplicationName) &&
        !string.IsNullOrWhiteSpace(CyberArkClientCredential);

    private bool CanUseCyberArkProfile() =>
        CanManageCyberArk() &&
        SelectedCyberArkProfile is not null;

    private bool CanManageCyberArk() =>
        cyberArkService is not null &&
        !IsBusy &&
        IsApplicationReady;

    private bool CanUseCyberArkAccount() =>
        CanUseCyberArkProfile() &&
        SelectedCyberArkProfile is
        {
            IsEnabled: true,
            AuthenticationState: CyberArkAuthenticationState.Ready,
        } &&
        SelectedCyberArkAccount is not null &&
        !string.IsNullOrWhiteSpace(CyberArkRetrievalReason);

    private bool CanRemoveCyberArkProfile() =>
        CanUseCyberArkProfile() &&
        string.Equals(
            CyberArkRemoveConfirmation.Trim(),
            "REMOVE CYBERARK",
            StringComparison.Ordinal);

    private bool CanRevokeCyberArkProfile() =>
        CanUseCyberArkProfile() &&
        SelectedCyberArkProfile?.AuthenticationState !=
            CyberArkAuthenticationState.Revoked &&
        string.Equals(
            CyberArkRemoveConfirmation.Trim(),
            "REVOKE CYBERARK",
            StringComparison.Ordinal);

    private void NotifyCyberArkCommands()
    {
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
        SynchronizeCyberArkCommand.NotifyCanExecuteChanged();
        SearchCyberArkCommand.NotifyCanExecuteChanged();
        RevealCyberArkCommand.NotifyCanExecuteChanged();
        CopyCyberArkCommand.NotifyCanExecuteChanged();
        ToggleCyberArkProfileCommand.NotifyCanExecuteChanged();
        RevokeCyberArkProfileCommand.NotifyCanExecuteChanged();
        RemoveCyberArkProfileCommand.NotifyCanExecuteChanged();
        NewCyberArkProfileCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCyberArkProfileChanged(CyberArkProfile? value)
    {
        if (value is not null)
        {
            CyberArkProfileName = value.DisplayName;
            CyberArkIdentityUrl = value.IdentityUrl.AbsoluteUri;
            CyberArkPrivilegeCloudUrl = value.PrivilegeCloudUrl.AbsoluteUri;
            CyberArkServiceUserName = value.ServiceUserName;
            CyberArkApplicationName = value.ApplicationName;
            CyberArkClientCredential = string.Empty;
            _ = ReloadCyberArkSourceAsync(CancellationToken.None);
        }
        NotifyCyberArkCommands();
    }

    partial void OnSelectedCyberArkAccountChanged(CyberArkAccountRow? value)
    {
        HideCyberArkValue();
        if (value is not null)
            _ = ReloadCyberArkAccountDetailsAsync(value.Account);
        NotifyCyberArkCommands();
    }

    partial void OnSelectedCyberArkVersionChanged(CyberArkVersionRow? value)
    {
        HideCyberArkValue();
        NotifyCyberArkCommands();
    }

    partial void OnCyberArkProfileNameChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkIdentityUrlChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkPrivilegeCloudUrlChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkServiceUserNameChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkApplicationNameChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkClientCredentialChanged(string value) =>
        ConnectCyberArkCommand.NotifyCanExecuteChanged();
    partial void OnCyberArkRetrievalReasonChanged(string value) =>
        NotifyCyberArkCommands();
    partial void OnCyberArkRemoveConfirmationChanged(string value)
    {
        RevokeCyberArkProfileCommand.NotifyCanExecuteChanged();
        RemoveCyberArkProfileCommand.NotifyCanExecuteChanged();
    }
}

public sealed class CyberArkSafeRow(CyberArkSafe safe)
{
    public CyberArkSafe Safe { get; } = safe;
    public string Summary =>
        $"{Safe.Name} · retention {Safe.RetentionDays?.ToString(CultureInfo.InvariantCulture) ?? "policy"} days / {Safe.RetentionVersions?.ToString(CultureInfo.InvariantCulture) ?? "policy"} versions · OLAC {(Safe.ObjectLevelAccessControlEnabled ? "on" : "off")}";
}

public sealed class CyberArkAccountRow(CyberArkAccount account)
{
    public CyberArkAccount Account { get; } = account;
    public string Summary =>
        $"{Account.Name} · {Account.SafeName} · {Account.UserName ?? "no username"} · {Account.Address ?? "no address"} · {Account.SecretType}";
}

public sealed class CyberArkVersionRow(CyberArkSecretVersion version)
{
    public CyberArkSecretVersion Version { get; } = version;
    public string Summary =>
        $"Version {Version.VersionId} · {Version.ModifiedAt:u} · {(Version.IsTemporary ? "temporary" : "retained")} · modified by {Version.ModifiedBy}";
}

public sealed class CyberArkAuditRow(CyberArkAuditEvent auditEvent)
{
    public CyberArkAuditEvent AuditEvent { get; } = auditEvent;
    public string Summary =>
        $"{AuditEvent.OccurredAt:u} · {AuditEvent.Operation} · {AuditEvent.Result} · {AuditEvent.SafeName ?? "profile"} · {AuditEvent.SafeMessage}";
}
