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
        UnauthorizedAccessException => new(
            "Windows verification was not completed",
            "Vault Prospector did not reveal, copy, or cache the secret.",
            "Set up Windows Hello or complete the verification prompt and retry."),
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
            "Restore the matching Vault Prospector data and key backup under the same Windows account. If no matched backup exists, remove the local VaultProspector data folder and reconnect to Azure."),
        IncompatibleLocalDataVersionException => new(
            "A newer Vault Prospector version is required",
            "This installation is older than the encrypted local-data format and refused to modify it.",
            "Install the same or a newer Vault Prospector version than the one that last opened this data."),
        LocalDataIntegrityException => new(
            "Encrypted local metadata failed validation",
            "Vault Prospector preserved the encrypted database and stopped without rebuilding or using it.",
            "Keep the local data for support or restore a matched data-and-key backup. If recovery is not needed, remove the local VaultProspector data folder and reconnect to Azure."),
        AuthenticationTagMismatchException or CryptographicException => new(
            "Protected local data failed integrity verification",
            "Vault Prospector refused to use modified, corrupted, or incompatible protected data.",
            "Purge the affected offline copy. If startup fails, close the app and remove its local data before reconnecting."),
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
}
