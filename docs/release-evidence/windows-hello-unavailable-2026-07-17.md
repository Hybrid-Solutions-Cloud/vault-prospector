# Windows Hello unavailable-path validation — 2026-07-17

**Scope:** Production `WindowsHelloVerificationService` from source commit `a99fd41`

**Status:** Internal noninteractive fail-closed evidence only; not an interactive Windows Hello
success, cancellation, PIN, biometric, policy, or independent-review result

## Environment and provenance

The validation ran on the isolated `vp-win11-preview-test` Hyper-V guest used for clean Windows
testing. The guest is Windows 11 Enterprise Evaluation 25H2 x64, version `10.0.26200`, with Secure
Boot enabled and a present and ready TPM. The prior accessibility and installer test state had been
removed, and the original encrypted Vault Prospector user data remained restored.

A transient self-contained `win-x64` probe referenced the production
`VaultProspector.Platform.WindowsHelloVerificationService`. It recorded only process/session facts,
the `UserConsentVerifier` availability enum, whether verification was requested, the Boolean
result, and exception type if one occurred. It did not record a password, PIN, biometric, account,
tenant, token, or exception message.

| Probe artifact | SHA-256 |
| --- | --- |
| `vp-hello-probe.exe` | `E698EA7F0FE02FFB01E8D00CBCF6C0642DC44CF165A693F7FDD12873D1A61DD5` |
| transient `Program.cs` | `575563B013199D575F67118C19C6521743E04364551373636ED45F73933507F0` |
| transient project file | `6F3820FEEDFD8E35D5A84B2D534CFC1C2EAD2BBCA85ABCC8312819868248A48D` |

The executable hash was independently recomputed after transfer into the guest and matched the host
hash.

## Observed boundary

After the guest's earlier cleanup reboot, no Explorer desktop session was logged in. An
interactive-token scheduled task therefore did not start; its task result was `267011`, and no
probe process or output existed. This is recorded as setup evidence, not an application failure.

The probe was then run under the dedicated test account in a Task Scheduler password-logon session.
That session was noninteractive (`sessionId:0`, `userInteractive:false`). The availability-only run
returned:

```json
{"availability":"DeviceNotPresent","verificationRequested":false,"verified":null}
```

A second run invoked the production service's `VerifyAsync` method. It completed successfully at
`2026-07-17T13:43:34Z`, Task Scheduler reported exit code `0`, and the safe result was:

```json
{"sessionId":0,"userInteractive":false,"availability":"DeviceNotPresent","verificationRequested":true,"verified":false}
```

This proves the implemented unavailable-device boundary fails closed: when Windows does not make
user verification available to the calling session, Vault Prospector returns `false` and releases
no approval. It does not prove behavior in a logged-in session with Windows Hello configured.

## Cleanup verification

The credential-bearing scheduled task was unregistered immediately after the run. Final guest
inspection found no `VP-*` task, probe process, or `C:\VP-Runtime-Security` root. The exact transient
host project root `D:\tmp\vp-hello-probe` was removed after its source hashes were recorded. No
repository file, log, evidence record, or command output contains the test-account password.

## Remaining required evidence

P-05 and P-08 remain in progress. A logged-in supported Windows session must still exercise:

- Windows Hello availability with PIN and, where supported, biometric verification;
- successful approval and explicit cancellation;
- unavailable/not-configured, retry, lock-during-prompt, and policy-denied outcomes;
- proof that Azure retrieval, protected cache access, and clipboard mutation do not occur before a
  successful verification result;
- independent repetition against the final signed immutable candidate.
