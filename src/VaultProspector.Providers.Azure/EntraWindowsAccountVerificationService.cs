using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Identity.Client;
using VaultProspector.Application;

namespace VaultProspector.Providers.Azure;

public sealed class EntraWindowsAccountVerificationService
    : IUserVerificationService
{
    private readonly IEntraWindowsAccountVerificationInterop _interop;
    private readonly IDiagnosticSink? _diagnostics;

    public EntraWindowsAccountVerificationService(
        string clientId,
        IDiagnosticSink? diagnostics = null)
        : this(
            new EntraWindowsAccountVerificationInterop(clientId),
            diagnostics)
    {
    }

    internal EntraWindowsAccountVerificationService(
        IEntraWindowsAccountVerificationInterop interop,
        IDiagnosticSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(interop);
        _interop = interop;
        _diagnostics = diagnostics;
    }

    public bool IsAvailable => _interop.IsCurrentAccountEntra;

    public async Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = await _interop.VerifyCurrentAccountAsync(
            reason,
            cancellationToken);
        WriteDiagnostic(outcome);
        return outcome.Result;
    }

    private void WriteDiagnostic(
        EntraWindowsAccountVerificationOutcome outcome)
    {
        if (_diagnostics is null)
            return;

        var fields = new Dictionary<string, object?>
        {
            ["status"] = outcome.Status,
        };
        if (outcome.ErrorCategory is not null)
            fields["error_category"] = outcome.ErrorCategory;
        _diagnostics.Information(
            "windows_remote_verification_completed",
            fields);
    }
}

internal interface IEntraWindowsAccountVerificationInterop
{
    bool IsCurrentAccountEntra { get; }

    Task<EntraWindowsAccountVerificationOutcome>
        VerifyCurrentAccountAsync(
            string reason,
            CancellationToken cancellationToken);
}

internal sealed record EntraWindowsAccountVerificationOutcome(
    UserVerificationResult Result,
    string Status,
    string? ErrorCategory)
{
    public static EntraWindowsAccountVerificationOutcome Verified() =>
        new(UserVerificationResult.Verified, "authorized", null);

    public static EntraWindowsAccountVerificationOutcome Canceled() =>
        new(UserVerificationResult.Canceled, "cancelled", null);

    public static EntraWindowsAccountVerificationOutcome Failed(
        string errorCategory) =>
        new(
            UserVerificationResult.RemoteCredentialFailed,
            "failed",
            errorCategory);
}

internal sealed class EntraWindowsAccountVerificationInterop(
    string clientId) : IEntraWindowsAccountVerificationInterop
{
    private const int NameUserPrincipal = 8;
    private const string AzureAdPrefix = "AzureAD\\";
    private readonly string _clientId =
        !string.IsNullOrWhiteSpace(clientId)
            ? clientId
            : throw new ArgumentException(
                "A public-client application ID is required.",
                nameof(clientId));

    public bool IsCurrentAccountEntra
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                using var currentIdentity = WindowsIdentity.GetCurrent();
                return currentIdentity.Name.StartsWith(
                           AzureAdPrefix,
                           StringComparison.OrdinalIgnoreCase) &&
                       TryConvertAzureAdSidToObjectId(
                           currentIdentity.User?.Value,
                           out _);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                    UnauthorizedAccessException or
                    System.Security.SecurityException)
            {
                // An account that cannot be identified as Entra-backed is
                // never routed into interactive Entra verification.
                return false;
            }
        }
    }

    public async Task<EntraWindowsAccountVerificationOutcome>
        VerifyCurrentAccountAsync(
            string reason,
            CancellationToken cancellationToken)
    {
        _ = reason;
        if (!TryGetCurrentIdentity(out var currentIdentity))
        {
            return EntraWindowsAccountVerificationOutcome.Failed(
                "current_identity_unavailable");
        }

        try
        {
            var application = PublicClientApplicationBuilder
                .Create(_clientId)
                .WithAuthority(
                    AzureCloudInstance.AzurePublic,
                    AadAuthorityAudience.AzureAdMultipleOrgs)
                .WithDefaultRedirectUri()
                .Build();
            var result = await application
                .AcquireTokenInteractive(
                    AzureAuthenticationScopes.InteractiveSignIn)
                .WithLoginHint(currentIdentity.UserPrincipalName)
                .WithPrompt(Prompt.ForceLogin)
                .ExecuteAsync(cancellationToken);

            return Guid.TryParse(result.UniqueId, out var verifiedObjectId) &&
                   verifiedObjectId == currentIdentity.ObjectId
                ? EntraWindowsAccountVerificationOutcome.Verified()
                : EntraWindowsAccountVerificationOutcome.Failed(
                    "sid_mismatch");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MsalClientException exception)
            when (string.Equals(
                exception.ErrorCode,
                "authentication_canceled",
                StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(
                      exception.ErrorCode,
                      "user_canceled",
                      StringComparison.OrdinalIgnoreCase))
        {
            return EntraWindowsAccountVerificationOutcome.Canceled();
        }
        catch (MsalException)
        {
            return EntraWindowsAccountVerificationOutcome.Failed(
                "interactive_authentication_failed");
        }
        catch (InvalidOperationException)
        {
            return EntraWindowsAccountVerificationOutcome.Failed(
                "interactive_authentication_failed");
        }
    }

    internal static bool TryConvertAzureAdSidToObjectId(
        string? sid,
        out Guid objectId)
    {
        objectId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(sid))
            return false;

        var components = sid.Split('-');
        if (components.Length != 8 ||
            !string.Equals(components[0], "S", StringComparison.OrdinalIgnoreCase) ||
            components[1] != "1" ||
            components[2] != "12" ||
            components[3] != "1")
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[16];
        for (var index = 0; index < 4; index++)
        {
            if (!uint.TryParse(components[index + 4], out var component))
                return false;
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.Slice(index * sizeof(uint), sizeof(uint)),
                component);
        }

        objectId = new Guid(bytes);
        return true;
    }

    private static bool TryGetCurrentIdentity(
        out CurrentEntraWindowsIdentity identity)
    {
        identity = default;
        if (!OperatingSystem.IsWindows())
            return false;

        using var currentIdentity = WindowsIdentity.GetCurrent();
        if (!currentIdentity.Name.StartsWith(
                AzureAdPrefix,
                StringComparison.OrdinalIgnoreCase) ||
            !TryConvertAzureAdSidToObjectId(
                currentIdentity.User?.Value,
                out var objectId) ||
            !TryGetCurrentUserPrincipalName(out var userPrincipalName))
        {
            return false;
        }

        identity = new CurrentEntraWindowsIdentity(
            objectId,
            userPrincipalName);
        return true;
    }

    private static bool TryGetCurrentUserPrincipalName(
        out string userPrincipalName)
    {
        userPrincipalName = string.Empty;
        uint capacity = 0;
        _ = GetUserNameEx(NameUserPrincipal, null, ref capacity);
        if (capacity == 0)
            return false;

        var buffer = new char[checked((int)capacity)];
        if (!GetUserNameEx(NameUserPrincipal, buffer, ref capacity))
            return false;

        userPrincipalName = new string(
            buffer,
            0,
            checked((int)capacity));
        return !string.IsNullOrWhiteSpace(userPrincipalName);
    }

    [DllImport(
        "secur32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "GetUserNameExW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserNameEx(
        int nameFormat,
        [Out] char[]? userName,
        ref uint userNameSize);

    private readonly record struct CurrentEntraWindowsIdentity(
        Guid ObjectId,
        string UserPrincipalName);
}
