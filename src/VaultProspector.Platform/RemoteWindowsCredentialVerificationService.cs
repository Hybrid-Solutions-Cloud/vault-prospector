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

    public RemoteWindowsCredentialVerificationService(
        Func<nint> windowHandleProvider)
        : this(
            windowHandleProvider,
            new RemoteWindowsCredentialInterop())
    {
    }

    internal RemoteWindowsCredentialVerificationService(
        Func<nint> windowHandleProvider,
        IRemoteWindowsCredentialInterop interop)
    {
        ArgumentNullException.ThrowIfNull(windowHandleProvider);
        ArgumentNullException.ThrowIfNull(interop);
        _windowHandleProvider = windowHandleProvider;
        _interop = interop;
    }

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task<UserVerificationResult> VerifyAsync(
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var windowHandle = _windowHandleProvider();
        if (windowHandle == 0)
            return Task.FromResult(UserVerificationResult.Unavailable);

        return Task.FromResult(
            _interop.VerifyCurrentUser(windowHandle, reason));
    }
}

internal interface IRemoteWindowsCredentialInterop
{
    UserVerificationResult VerifyCurrentUser(
        nint windowHandle,
        string reason);
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

    public UserVerificationResult VerifyCurrentUser(
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
                return UserVerificationResult.Canceled;
            if (promptResult != 0)
                return UserVerificationResult.RemoteCredentialUnavailable;

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
                return UserVerificationResult.RemoteCredentialFailed;

            var userName =
                Marshal.PtrToStringUni(userNameBuffer) ?? string.Empty;
            var domain = Marshal.PtrToStringUni(domainBuffer);
            if (string.IsNullOrWhiteSpace(userName))
                return UserVerificationResult.RemoteCredentialFailed;

            var loggedOn = LogonUser(
                    userName,
                    string.IsNullOrWhiteSpace(domain) ? null : domain,
                    passwordBuffer,
                    Logon32LogonNetwork,
                    Logon32ProviderDefault,
                    out var token);

            using (token)
            {
                if (!loggedOn)
                    return UserVerificationResult.RemoteCredentialFailed;

                using var verifiedIdentity =
                    new WindowsIdentity(token.DangerousGetHandle());
                using var currentIdentity = WindowsIdentity.GetCurrent();
                var verifiedSid = verifiedIdentity.User;
                var currentSid = currentIdentity.User;
                return verifiedSid is not null &&
                       currentSid is not null &&
                       verifiedSid.Equals(currentSid)
                    ? UserVerificationResult.Verified
                    : UserVerificationResult.RemoteCredentialFailed;
            }
        }
        catch (Exception exception) when (
            exception is Win32Exception or
                ExternalException or
                InvalidOperationException or
                UnauthorizedAccessException)
        {
            return UserVerificationResult.RemoteCredentialFailed;
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
