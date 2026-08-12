using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class RemoteWindowsCredentialVerificationService :
    IUserVerificationService
{
    private readonly Func<nint> _windowHandleProvider;
    private readonly IRemoteWindowsCredentialInterop _interop;
    private readonly IDiagnosticSink? _diagnostics;

    public RemoteWindowsCredentialVerificationService(
        Func<nint> windowHandleProvider,
        IDiagnosticSink? diagnostics = null)
        : this(
            windowHandleProvider,
            new RemoteWindowsCredentialInterop(),
            diagnostics)
    {
    }

    internal RemoteWindowsCredentialVerificationService(
        Func<nint> windowHandleProvider,
        IRemoteWindowsCredentialInterop interop,
        IDiagnosticSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(windowHandleProvider);
        ArgumentNullException.ThrowIfNull(interop);
        _windowHandleProvider = windowHandleProvider;
        _interop = interop;
        _diagnostics = diagnostics;
    }

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var windowHandle = _windowHandleProvider();
        if (windowHandle == 0)
        {
            WriteDiagnostic(
                new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.Unavailable,
                    "failed",
                    "prompt_unavailable"));
            return Task.FromResult(UserVerificationResult.Unavailable);
        }

        var outcome = _interop.VerifyCurrentUser(windowHandle, reason);
        WriteDiagnostic(outcome);
        return Task.FromResult(outcome.Result);
    }

    private void WriteDiagnostic(
        RemoteWindowsCredentialVerificationOutcome outcome)
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

internal interface IRemoteWindowsCredentialInterop
{
    RemoteWindowsCredentialVerificationOutcome VerifyCurrentUser(
        nint windowHandle,
        string reason);
}

internal sealed record RemoteWindowsCredentialVerificationOutcome(
    UserVerificationResult Result,
    string Status,
    string? ErrorCategory)
{
    public static RemoteWindowsCredentialVerificationOutcome FromResult(
        UserVerificationResult result) => result switch
        {
            UserVerificationResult.Verified =>
                new(result, "authorized", null),
            UserVerificationResult.Canceled =>
                new(result, "cancelled", null),
            UserVerificationResult.RemoteCredentialUnavailable =>
                new(result, "failed", "prompt_unavailable"),
            _ => new(result, "failed", "credential_rejected"),
        };
}

internal sealed class RemoteWindowsCredentialInterop :
    IRemoteWindowsCredentialInterop
{
    private const int ErrorCancelled = 1223;
    private const int Logon32LogonNetwork = 3;
    private const int Logon32ProviderDefault = 0;
    private const uint CreduiwinGeneric = 0x1;
    private const int MaximumUserNameCharacters = 513;
    private const int MaximumDomainCharacters = 256;
    private const int MaximumPasswordCharacters = 256;

    public RemoteWindowsCredentialVerificationOutcome VerifyCurrentUser(
        nint windowHandle,
        string reason)
    {
        nint authenticationBuffer = 0;
        nint userNameBuffer = 0;
        nint domainBuffer = 0;
        nint passwordBuffer = 0;
        uint authenticationBufferSize = 0;
        try
        {
            var info = new CredUiInfo
            {
                Size = Marshal.SizeOf<CredUiInfo>(),
                Parent = windowHandle,
                CaptionText = "Unlock Vault Prospector",
                MessageText =
                    $"{reason}. Enter the credentials for your current Windows account.",
            };
            uint authenticationPackage = 0;
            var save = false;
            var promptResult = CredUIPromptForWindowsCredentials(
                ref info,
                0,
                ref authenticationPackage,
                0,
                0,
                out authenticationBuffer,
                out authenticationBufferSize,
                ref save,
                CreduiwinGeneric);
            if (promptResult == ErrorCancelled)
                return RemoteWindowsCredentialVerificationOutcome.FromResult(
                    UserVerificationResult.Canceled);
            if (promptResult != 0)
            {
                return new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.RemoteCredentialUnavailable,
                    "failed",
                    "prompt_unavailable");
            }

            userNameBuffer = AllocateCharacters(
                MaximumUserNameCharacters);
            domainBuffer = AllocateCharacters(MaximumDomainCharacters);
            passwordBuffer = AllocateCharacters(
                MaximumPasswordCharacters);
            uint userNameLength = MaximumUserNameCharacters;
            uint domainLength = MaximumDomainCharacters;
            uint passwordLength = MaximumPasswordCharacters;
            var unpacked = CredUnPackAuthenticationBuffer(
                0,
                authenticationBuffer,
                authenticationBufferSize,
                userNameBuffer,
                ref userNameLength,
                domainBuffer,
                ref domainLength,
                passwordBuffer,
                ref passwordLength);
            if (!unpacked)
            {
                return new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.RemoteCredentialFailed,
                    "failed",
                    "credential_unpack_failed");
            }

            var userName =
                Marshal.PtrToStringUni(userNameBuffer) ?? string.Empty;
            var domain = Marshal.PtrToStringUni(domainBuffer);
            if (string.IsNullOrWhiteSpace(userName))
            {
                return new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.RemoteCredentialFailed,
                    "failed",
                    "credential_unpack_failed");
            }

            var logonName = NormalizeLogonName(userName, domain);
            using var currentIdentity = WindowsIdentity.GetCurrent();
            var currentSid = currentIdentity.User;
            if (currentSid is null)
            {
                return new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.RemoteCredentialFailed,
                    "failed",
                    "native_failure");
            }

            logonName = NormalizeEntraLogonName(
                logonName,
                currentIdentity.Name);
            var loggedOn = LogonUser(
                    logonName.UserName,
                    logonName.Domain,
                    passwordBuffer,
                    Logon32LogonNetwork,
                    Logon32ProviderDefault,
                    out var token);

            using (token)
            {
                if (!loggedOn)
                {
                    return new RemoteWindowsCredentialVerificationOutcome(
                        UserVerificationResult.RemoteCredentialFailed,
                        "failed",
                        "credential_rejected");
                }

                using var verifiedIdentity =
                    new WindowsIdentity(token.DangerousGetHandle());
                var verifiedSid = verifiedIdentity.User;
                return verifiedSid is not null &&
                       verifiedSid.Equals(currentSid)
                    ? RemoteWindowsCredentialVerificationOutcome.FromResult(
                        UserVerificationResult.Verified)
                    : new RemoteWindowsCredentialVerificationOutcome(
                        UserVerificationResult.RemoteCredentialFailed,
                        "failed",
                        "sid_mismatch");
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or
                ExternalException or
                InvalidOperationException or
                UnauthorizedAccessException)
        {
            return new RemoteWindowsCredentialVerificationOutcome(
                UserVerificationResult.RemoteCredentialFailed,
                "failed",
                "native_failure");
        }
        finally
        {
            ZeroAndFree(
                passwordBuffer,
                MaximumPasswordCharacters * sizeof(char));
            ZeroAndFree(
                domainBuffer,
                MaximumDomainCharacters * sizeof(char));
            ZeroAndFree(
                userNameBuffer,
                MaximumUserNameCharacters * sizeof(char));
            ZeroAndFreeCoTaskMem(
                authenticationBuffer,
                authenticationBuffer == 0
                    ? 0
                    : checked((int)authenticationBufferSize));
        }
    }

    internal static (string UserName, string? Domain) NormalizeLogonName(
        string userName,
        string? domain)
    {
        var normalizedDomain =
            string.IsNullOrWhiteSpace(domain) ? null : domain;
        if (normalizedDomain is not null)
            return (userName, normalizedDomain);

        var separatorIndex = userName.IndexOf('\\');
        if (separatorIndex <= 0 ||
            separatorIndex == userName.Length - 1)
        {
            return (userName, null);
        }

        return (
            userName[(separatorIndex + 1)..],
            userName[..separatorIndex]);
    }

    internal static (string UserName, string? Domain)
        NormalizeEntraLogonName(
            (string UserName, string? Domain) logonName,
            string? currentIdentityName)
    {
        if (!string.IsNullOrWhiteSpace(logonName.Domain))
            return logonName;

        var currentName = NormalizeLogonName(
            currentIdentityName ?? string.Empty,
            null);
        // Credential UI can return an Entra UPN or account alias without its
        // authority. Windows 10/11 may reject that otherwise-valid credential
        // unless AzureAD is supplied as the domain. This is applied only when
        // the current process identity is already Entra-backed; the verified
        // token must still match the current SID below.
        return string.Equals(
                currentName.Domain,
                "AzureAD",
                StringComparison.OrdinalIgnoreCase)
            ? (logonName.UserName, "AzureAD")
            : logonName;
    }

    private static nint AllocateCharacters(int characterCount) =>
        Marshal.AllocHGlobal(characterCount * sizeof(char));

    private static void ZeroAndFree(nint buffer, int byteCount)
    {
        if (buffer == 0)
            return;
        Zero(buffer, byteCount);
        Marshal.FreeHGlobal(buffer);
    }

    private static void ZeroAndFreeCoTaskMem(
        nint buffer,
        int byteCount)
    {
        if (buffer == 0)
            return;
        Zero(buffer, byteCount);
        Marshal.FreeCoTaskMem(buffer);
    }

    private static void Zero(nint buffer, int byteCount)
    {
        for (var offset = 0; offset < byteCount; offset++)
            Marshal.WriteByte(buffer, offset, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CredUiInfo
    {
        public int Size;
        public nint Parent;
        public string? MessageText;
        public string? CaptionText;
        public nint Banner;
    }

    [DllImport(
        "credui.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "CredUIPromptForWindowsCredentialsW")]
    private static extern int CredUIPromptForWindowsCredentials(
        ref CredUiInfo uiInfo,
        int authenticationError,
        ref uint authenticationPackage,
        nint inputAuthenticationBuffer,
        uint inputAuthenticationBufferSize,
        out nint outputAuthenticationBuffer,
        out uint outputAuthenticationBufferSize,
        ref bool save,
        uint flags);

    [DllImport(
        "credui.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "CredUnPackAuthenticationBufferW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredUnPackAuthenticationBuffer(
        uint flags,
        nint authenticationBuffer,
        uint authenticationBufferSize,
        nint userName,
        ref uint maximumUserName,
        nint domainName,
        ref uint maximumDomainName,
        nint password,
        ref uint maximumPassword);

    [DllImport(
        "advapi32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "LogonUserW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string userName,
        string? domain,
        nint password,
        int logonType,
        int logonProvider,
        out SafeAccessTokenHandle token);
}
