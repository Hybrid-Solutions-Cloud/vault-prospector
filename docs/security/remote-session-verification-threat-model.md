# Remote-session verification threat model

## Decision

Vault Prospector continues to use window-bound Windows Hello verification whenever Windows reports
it available. In an AVD or Remote Desktop session where Windows reports `DeviceNotPresent`, the
application selects a fallback based on the **currently signed-in Windows account**:

- a Microsoft Entra-backed account completes a fresh system-browser Entra sign-in capable of
  satisfying MFA and Conditional Access; or
- a local or Active Directory domain account completes the native Windows credential prompt.

The remote fallback:

- is available only after Windows Hello specifically reports the remote-session limitation;
- is disabled by `DisableRemoteCredentialVerification=1` in machine policy;
- never unlocks when policy is invalid or unreadable;
- converts the current `S-1-12-1` Windows cloud SID to its Entra object ID and requires the
  interactively authenticated Entra object ID to match exactly;
- does not persist the verification token or add the verified account as a connected application
  identity;
- validates local/domain credentials with Windows and compares the resulting token SID to the
  current process user's SID;
- rejects credentials for every other local, domain, or Microsoft Entra account;
- never stores, logs, transmits, or returns a local/domain password; and
- zeroes the native authentication, user, domain, and password buffers before releasing them.

This is a current-Windows-account verification boundary. The Entra route authenticates only to
verify user presence and account identity; it does not change the DPAPI account/device binding of
local data, grant access to application data for another account, or permit unattended access.

## Threats and controls

| Threat | Control | Evidence |
| --- | --- | --- |
| A remote session silently bypasses verification | Fallback starts only after the normal verifier returns the explicit remote-device-unavailable result; every other failure remains unchanged. | Deterministic orchestration tests |
| A different account unlocks another user's data | Entra object ID must equal the object ID encoded in the current cloud SID; local/domain token SID must equal the current process SID. | SID conversion, routing, provider, and exact-package negative tests |
| Conditional Access requires MFA in an Entra-only VDI | Entra sessions use interactive authentication that can satisfy tenant MFA/Conditional Access instead of password-only `LogonUserW`. | Correlated Windows/AAD diagnosis and exact-package Entra RDP test |
| An Entra token becomes a persistent application identity | The verification client and cache are ephemeral; the result is used only for the object-ID comparison and is not saved to the product identity store. | Source review and post-unlock identity-store test |
| An administrator requires Windows Hello only | Machine policy can disable remote credential verification; invalid policy fails closed. | Policy parser, ADMX/ADML, and allowed/denied tests |
| Password material is retained | Native buffers are bounded, used only for the Windows logon call, overwritten before release, and never converted to a managed password string. | Source review and independent security review |
| Credentials are captured by an untrusted window | Local/domain credential UI is parented to the active Vault Prospector HWND. Entra credentials and MFA are entered in the system browser. Executable trust still depends on the installed package and Windows desktop integrity. | Exact-package UI test |
| Brute force or account lockout | Authentication is delegated to Windows, so domain/local account lockout and audit policy apply. The application adds no retry loop or credential cache. | Windows security-event and policy validation |
| Remote verification changes console behavior | Local sessions never invoke the fallback. Windows Hello result handling remains the existing path. | Local-console regression tests |

## Failure behavior

Cancel returns to the locked screen. An unavailable prompt, interactive authentication failure,
invalid credential, object-ID/SID mismatch, native failure, or policy denial leaves the application
locked with actionable, non-sensitive guidance. There is no automatic retry and no unverified
recovery path.

## Required live validation

Before closing remote-verification work, including AB#7337, validate the exact packaged candidate
in:

1. a local Windows 11 console with Windows Hello success and cancellation;
2. Remote Desktop with the current local Windows account's valid credential;
3. Remote Desktop with the current Active Directory domain account's valid credential;
4. Remote Desktop with fresh interactive authentication and MFA for the current Microsoft Entra
   account;
5. Remote Desktop cancellation and a different Entra account, plus invalid/different local and
   domain credentials where those account types are deployed;
6. Remote Desktop with `DisableRemoteCredentialVerification=1`; and
7. an Entra-only AVD, Windows 365, or equivalent supported multi-session Windows host with no
   user-accessible local-account workaround.

Retain the candidate hash, machine/session details, policy state, safe outcome, and test time. Do
not retain submitted credentials.

Password-only Windows logon is retained only for local/domain accounts. Microsoft documents that
Entra Windows logon can require special authority handling, but correlated failure evidence for
AB#7337 additionally required MFA and therefore an interactive Entra flow. See
[CreateProcessWithLogonW fails when called on a Microsoft Entra account](https://learn.microsoft.com/troubleshoot/windows/win32/createprocesswithlogonw-fails-microsoft-entra-id-account).
