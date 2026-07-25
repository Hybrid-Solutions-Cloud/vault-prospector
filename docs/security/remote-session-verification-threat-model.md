# Remote-session verification threat model

## Decision

Vault Prospector continues to use window-bound Windows Hello verification whenever Windows reports
it available. In an AVD or Remote Desktop session where Windows reports
`DeviceNotPresent`, the application may instead display the Windows credential UI and validate the
credentials for the **currently signed-in Windows account**.

The remote fallback:

- is available only after Windows Hello specifically reports the remote-session limitation;
- is disabled by `DisableRemoteCredentialVerification=1` in machine policy;
- never unlocks when policy is invalid or unreadable;
- validates the submitted credential with Windows and compares the resulting token SID to the
  current process user's SID;
- rejects credentials for every other local, domain, or Microsoft Entra account;
- never stores, logs, transmits, or returns the password; and
- zeroes the native authentication, user, domain, and password buffers before releasing them.

This is a local Windows account-verification boundary. It does not authenticate to Azure, change
the DPAPI account/device binding of local data, or permit unattended access.

## Threats and controls

| Threat | Control | Evidence |
| --- | --- | --- |
| A remote session silently bypasses verification | Fallback starts only after the normal verifier returns the explicit remote-device-unavailable result; every other failure remains unchanged. | Deterministic orchestration tests |
| A different administrator account unlocks another user's data | Windows validates the credential, then the returned token SID must equal the current process user's SID. | Provider implementation and exact-package negative test |
| An administrator requires Windows Hello only | Machine policy can disable remote credential verification; invalid policy fails closed. | Policy parser, ADMX/ADML, and allowed/denied tests |
| Password material is retained | Native buffers are bounded, used only for the Windows logon call, overwritten before release, and never converted to a managed password string. | Source review and independent security review |
| Credentials are captured by an untrusted window | The Windows credential dialog is parented to the active Vault Prospector HWND; executable trust still depends on the installed package and Windows desktop integrity. | Exact-package UI test |
| Brute force or account lockout | Authentication is delegated to Windows, so domain/local account lockout and audit policy apply. The application adds no retry loop or credential cache. | Windows security-event and policy validation |
| Remote verification changes console behavior | Local sessions never invoke the fallback. Windows Hello result handling remains the existing path. | Local-console regression tests |

## Failure behavior

Cancel returns to the locked screen. An unavailable prompt, invalid credential, SID mismatch, native
failure, or policy denial leaves the application locked with actionable, non-sensitive guidance.
There is no automatic retry and no unverified recovery path.

## Required live validation

Before closing AB#5575 / GitHub issue 42, validate the exact packaged candidate in:

1. a local Windows 11 console with Windows Hello success and cancellation;
2. Remote Desktop with the current account's valid credential;
3. Remote Desktop with an invalid credential and with another account;
4. Remote Desktop with `DisableRemoteCredentialVerification=1`; and
5. an AVD session or an equivalent supported multi-session Windows host.

Retain the candidate hash, machine/session details, policy state, safe outcome, and test time. Do
not retain submitted credentials.
