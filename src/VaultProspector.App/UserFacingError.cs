using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Microsoft.Identity.Client;
using VaultProspector.Application;

namespace VaultProspector.App;

public sealed record UserFacingError(string Title, string Message, string Recovery);

public static class UserFacingErrorMapper
{
    public static UserFacingError From(Exception exception) => exception switch
    {
        MsalClientException { ErrorCode: "authentication_canceled" } => new(
            "Sign-in cancelled",
            "Microsoft sign-in was closed before the connection completed.",
            "Choose Continue to Microsoft sign-in when you are ready."),
        MsalUiRequiredException => new(
            "Microsoft sign-in required",
            "The selected identity needs a new interactive Microsoft Entra session.",
            "Reconnect the identity and complete any MFA or Conditional Access prompts."),
        AuthenticationFailedException { InnerException: MsalUiRequiredException } => new(
            "Microsoft sign-in required",
            "The selected identity needs a new interactive Microsoft Entra session.",
            "Reconnect the identity and complete any MFA or Conditional Access prompts."),
        AuthenticationFailedException => new(
            "Microsoft authentication failed",
            "Azure could not use the selected identity without a new sign-in.",
            "Reconnect the identity. If your organization blocks the product registration, enable the custom registration option."),
        RequestFailedException { Status: 401 } => new(
            "Azure session is no longer authorized",
            "Azure rejected the current access token.",
            "Reconnect the selected identity and try again."),
        RequestFailedException { Status: 403 } => new(
            "Azure access is not permitted",
            "The selected identity can reach Azure but lacks permission for this resource or operation.",
            "Ask an Azure administrator for the minimum read permission required; Vault Prospector never grants roles."),
        RequestFailedException { Status: 404 } => new(
            "Azure item was not found",
            "The selected resource or version may have been removed after the last synchronization.",
            "Synchronize the identity, then select a current result."),
        RequestFailedException { Status: 429 } => new(
            "Azure is throttling requests",
            "Azure temporarily limited this operation.",
            "Wait a few minutes, then synchronize or retrieve the value again."),
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => new(
            "Microsoft Graph authorization expired",
            "Directory discovery could not use the selected interactive identity.",
            "Choose Authorize Microsoft Graph directory read and complete the Microsoft Entra prompt again."),
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => new(
            "Microsoft Graph directory read is not permitted",
            "The selected identity or app registration lacks the delegated Application.Read.All permission or required directory role.",
            "Ask a Microsoft Entra administrator to approve the least-privileged directory-read permission, then authorize again."),
        UnauthorizedAccessException => new(
            "Windows verification was not completed",
            "Vault Prospector did not reveal, copy, or cache the secret.",
            "Set up Windows Hello or complete the verification prompt and retry."),
        LocalDataResetConfirmationException => new(
            "Reset confirmation did not match",
            "Vault Prospector preserved all local data and did not begin recovery.",
            "Type RESET exactly, then complete fresh Windows verification."),
        LocalRecoveryArchiveConfirmationException => new(
            "Archive deletion confirmation did not match",
            "Vault Prospector retained the selected recovery archive.",
            "Select the intended archive and type DELETE ARCHIVE exactly."),
        LocalRecoveryArchiveVerificationException => new(
            "Archive deletion was not verified",
            "Vault Prospector retained the selected recovery archive.",
            "Retry only when you are ready to complete fresh Windows verification."),
        LocalRecoveryArchiveValidationException => new(
            "Recovery archive was preserved",
            "Vault Prospector could not validate the selected archive as a safe deletion target.",
            "Refresh the archive list. If the warning remains, preserve the folder and use the support guidance."),
        WorkloadIdentityConfigurationException => new(
            "Workload identity settings need attention",
            "The managed-identity or service-principal profile is incomplete or invalid.",
            "Use managed identity only on a detected Azure host. For a service principal, enter GUID tenant and client IDs plus either a valid certificate thumbprint or a readable federated token file path; client secrets are not accepted."),
        WorkloadCredentialUnavailableException => new(
            "Workload credential is unavailable",
            "Vault Prospector could not use the configured managed identity, certificate, or federated credential.",
            "Confirm the Azure host identity, install a currently valid certificate with an accessible private key, or restore the configured federated token file and issuer trust."),
        LocalRevocationCleanupException => new(
            "Local access was revoked; offline cleanup needs attention",
            "Vault Prospector disabled the identity and removed its stored credential reference, but could not purge every associated offline vault cache.",
            "Open Settings and choose Purge all offline values before using the device unattended. Revoke the external credential at its issuer if it may be compromised."),
        WorkloadAuthorizationEvidenceException { StatusCode: 401 } => new(
            "Azure authorization session expired",
            "Azure could not use the selected administrator to read authorization evidence.",
            "Reauthenticate the selected interactive identity, then run the assessment again."),
        WorkloadAuthorizationEvidenceException { StatusCode: 403 } => new(
            "Azure authorization evidence is not permitted",
            "The selected administrator cannot read all role-assignment or resource evidence required for this assessment.",
            "Grant the minimum Azure read permissions at the exact identity and Key Vault scopes. Confirmed deny analysis additionally requires Microsoft.Authorization/denyAssignments/read."),
        WorkloadAuthorizationEvidenceException => new(
            "Azure authorization evidence is unavailable",
            "Azure did not return the role-assignment or resource evidence required for this assessment.",
            "Verify the exact Key Vault resource ID and Azure availability, then retry without changing any roles."),
        ArgumentException argumentException when IsWorkloadAdministrationParameter(argumentException.ParamName) => new(
            "Workload administration scope needs attention",
            "The subscription, resource name, Key Vault scope, or role definition is incomplete or invalid.",
            "Use GUID tenant/subscription identifiers and exact matching Key Vault and Microsoft.Authorization role-definition resource IDs. Vault and role must be supplied together."),
        ArgumentException => new(
            "Connection settings need attention",
            "The custom Microsoft Entra application ID is missing or invalid.",
            "Use the recommended Vault Prospector registration, or enter the Application (client) ID from your organization's public-client registration."),
        KeyNotFoundException => new(
            "The selected value is unavailable",
            "The indexed item or its unexpired offline copy no longer exists.",
            "Synchronize Azure metadata, select a current item, or cache the value again explicitly."),
        PlatformNotSupportedException => new(
            "Required Windows security is unavailable",
            "This operation requires Windows Hello and DPAPI in a supported Windows user session.",
            "Use a supported Windows 10 or Windows 11 session and configure Windows Hello."),
        ProtectedKeyUnavailableException => new(
            "Protected local data key is unavailable",
            "Vault Prospector stopped without replacing the missing Windows-protected key or changing encrypted local data.",
            "Close and reopen Vault Prospector to use protected local-data recovery. Restore a matching data-and-key set under the same Windows account, or use the verified archive-and-reset action when starting fresh is deliberate."),
        IncompatibleLocalDataVersionException => new(
            "A newer Vault Prospector version is required",
            "This installation is older than the encrypted local-data format and refused to modify it.",
            "Install the same or a newer Vault Prospector version than the one that last opened this data."),
        LocalDataIntegrityException => new(
            "Encrypted local metadata failed validation",
            "Vault Prospector preserved the encrypted database and stopped without rebuilding or using it.",
            "Close and reopen Vault Prospector to preserve the current state for support, restore a matched data-and-key set, or use the verified archive-and-reset action when starting fresh is deliberate."),
        AuthenticationTagMismatchException or CryptographicException => new(
            "Protected local data failed integrity verification",
            "Vault Prospector refused to use modified, corrupted, or incompatible protected data.",
            "Purge the affected offline copy. If startup fails, close and reopen Vault Prospector and use its protected recovery workflow; never delete the database or key files separately."),
        JsonException => new(
            "Local settings could not be read",
            "Vault Prospector stopped before using a damaged or incompatible settings file.",
            "Close the app, delete %LOCALAPPDATA%\\VaultProspector\\settings.json, then reopen it. Encrypted metadata and offline values are not deleted."),
        InvalidOperationException => new(
            "The action is blocked by current policy",
            "The selected item or security policy does not allow this operation.",
            "Select a compatible secret or review the explicit offline-cache and clipboard settings."),
        _ => new(
            "Operation failed safely",
            "Vault Prospector stopped the operation without displaying sensitive error details.",
            "Retry once. If it continues, review the local redacted diagnostic log and support guidance."),
    };

    private static bool IsWorkloadAdministrationParameter(string? parameterName) =>
        parameterName is "tenantId"
            or "subscriptionId"
            or "resourceGroupName"
            or "identityName"
            or "keyVaultResourceId"
            or "keyVaultRoleDefinitionId";
}
