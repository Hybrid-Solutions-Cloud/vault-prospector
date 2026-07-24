# Independent security review plan

## Purpose and release boundary

This plan defines the evidence required to pass Preview gate P-08. It is a review brief, not a
security approval. The reviewer must be independent of the implementation under assessment, record
the exact commit and artifacts tested, and track every finding to a documented disposition.

P-08 passes only when no critical or high finding remains open. General Availability requires a
fresh or explicitly refreshed assessment against the final signed candidate, closure of all
critical and high findings, and written acceptance of any residual medium risk by a named owner.

## Reviewer independence

The reviewer must not be the person or automated agent that implemented the candidate changes. The
review record must identify the reviewer, organization or team, review dates, relevant experience,
conflicts of interest, and exact scope. Internal HCS personnel are acceptable only when they did not
author the reviewed implementation and can make an independent finding without release pressure.

## Candidate pinning

Before testing, record:

- source commit SHA and clean `git status` output;
- CI run URL and terminal result for every required job;
- MSI, application executable, portable archive, SBOM, and checksum SHA-256 values;
- Authenticode signer, timestamp, and verification output when signed artifacts exist;
- Windows edition, version, build, Secure Boot, TPM, Windows Hello, and virtualization state;
- .NET SDK, WiX, WinGet, Chocolatey, and security-tool versions;
- Entra tenant policy, application-registration mode, test identities, and Azure role names without
  recording tenant IDs, account names, tokens, object values, or other live identifiers.

Preview review may begin against an unsigned CI candidate to find defects early, but P-08 cannot be
used as final signed-candidate evidence unless the reviewer repeats artifact-sensitive checks after
signing.

## Required code-review scope

Review the following boundaries and their tests:

| Boundary | Primary implementation | Required questions |
| --- | --- | --- |
| Identity and tokens | `VaultProspector.Providers.Azure`, `IdentityService` | Is MSAL isolated from terminal caches? Are public-client IDs canonicalized? Are accounts removed on user removal and rolled back after failed metadata persistence? Can tenant or account context be confused? |
| Azure authorization | `AzureVaultProvider`, `SecretAccessService` | Can indexing retrieve a value? Can keys or certificate private material be exported? Can a broader identity or write operation be selected silently? Are ARM and Key Vault token audiences distinct? |
| Local metadata | `EncryptedSqliteMetadataRepository`, Windows DPAPI key provider | Does storage fail closed without DPAPI? Is the SQLCipher key handled as narrowly as the managed provider permits? Are queries parameterized, migrations safe, and file/key permissions appropriate? |
| Offline values | `EncryptedFileValueStore`, `CachePolicy` | Are payloads authenticated and encrypted, metadata bound as associated data, expiry and fingerprint changes enforced, replacement atomic, purge scopes correct, and malformed/tampered files fail-closed? |
| User verification | `WindowsHelloVerificationService`, `SecretAccessService` | Does every reveal, copy, cache, and offline open require a fresh successful OS verification? What happens on cancellation, lock, unavailable hardware, policy denial, and verification failure? |
| Secret lifetime and UI | `SensitiveValue`, `MainViewModel` | Are decrypted buffers disposed on success and every exceptional path? Are values masked, automatically hidden, absent from exceptions/diagnostics, and cleared during cancellation and shutdown? |
| Local key rotation | `LocalEncryptionRotationEngine`, `WindowsDataProtectionKeyProvider`, `EncryptedFileValueStore` | Does the verified archive remain a matched state set? Are journal/manifest authentication, SQLCipher rekey, offline re-encryption, staged key publication, rollback, path containment, memory clearing, and every crash boundary correct? |
| Clipboard | `AvaloniaClipboardService` | Is the interval valid, ownership serialized, plaintext retained only as required by the OS call, stale-clear behavior safe, unrelated content preserved, and exit cleanup best-effort and non-destructive? |
| Diagnostics and privacy | `RedactingDiagnosticSink`, error mapping, privacy docs | Are fields allowlisted and identifiers pseudonymized? Can exception text, tokens, names, values, paths, or clipboard/cache content enter logs or support output? |
| Installer and supply chain | workflows, packaging scripts, WiX, lock files | Are dependencies locked/scanned, workflows least-privileged, artifacts immutable, SBOM/provenance accurate, signatures verified, and package-manager metadata bound to the same installer hash? |

## Required adversarial and runtime tests

Use disposable, non-production tenants, vaults, values, identities, and Windows profiles. At minimum:

1. Tamper with every AES-GCM envelope field, substitute another item's envelope, truncate files,
   introduce invalid Base64/JSON, alter expiry/fingerprint/workspace/vault metadata, and test unknown
   and legacy key versions. No tampered value may be released.
2. Deny or cancel Windows Hello, make it unavailable, lock the session during the prompt, test PIN
   and biometric paths when available, and verify Azure/cache/clipboard services are untouched until
   verification succeeds.
3. Exercise MFA, Conditional Access success and denial, guest/resource tenants, revoked sessions,
   missing consent, token-cache deletion, identity removal, metadata-write failure after sign-in,
   and two identities using different client registrations. Confirm no Azure CLI, PowerShell, or IDE
   context is read or modified.
4. Use metadata-only and value-read roles to prove discovery and retrieval are separate. Attempt key,
   certificate, disabled-secret, wrong-vault, wrong-tenant, stale-version, and write operations.
5. Test copy timeout, rapid replacement, unrelated clipboard replacement, clipboard contention,
   application exit, Windows clipboard history, Remote Desktop clipboard, and cross-device clipboard
   behavior. Record platform retention that the app cannot revoke.
6. Force metadata/audit/cache/clipboard failures immediately before and after value acquisition.
   Inspect process memory and diagnostics for avoidable plaintext retention and prove exceptional
   paths dispose application-owned buffers.
7. Install, repair, upgrade, downgrade, roll back a deliberately failed update, uninstall with both
   retention choices, and verify signer trust, timestamping, hashes, SBOM, and package-manager
   installer identity on a clean supported Windows machine.

## Deterministic baseline commands

Run from a clean checkout with PowerShell 7:

```powershell
$env:Path = 'C:\Program Files\dotnet;' + $env:Path
pwsh ./scripts/Build.ps1
pwsh ./scripts/PackageInstaller.ps1 -Version <candidate-version>
pwsh ./scripts/PackageDistribution.ps1 -Version <candidate-version>
pwsh ./tests/scenario/windows-installer-lifecycle.scenario.ps1 `
    -PreviousMsiPath <previous-msi> `
    -PreviousSha256 <previous-sha256> `
    -CurrentMsiPath <candidate-msi> `
    -CurrentSha256 <candidate-sha256>
```

Also verify the full-history secret-scan job on the pinned CI run. Reviewer-specific static,
dependency, binary, memory, proxy, and malware-analysis tools must be named with versions and raw
reports retained outside the repository when they could contain machine paths or sensitive test
data.

## Finding and disposition record

Every finding must contain:

- identifier, title, severity, CWE or equivalent category, affected commit/artifact, and boundary;
- reproducible steps using synthetic values, observed result, expected result, and security impact;
- affected files and versions, proposed remediation, owner, target date, and retest evidence;
- status: open, remediated and independently verified, accepted residual risk, or not reproducible;
- for accepted risk, named approver, justification, compensating controls, expiration date, and
  tracking issue.

Critical and high findings cannot be risk-accepted for Preview or GA. Medium findings require an
explicit owner and disposition before GA. Reports must never contain live secrets, tokens, private
keys, raw tenant/subscription identifiers, or production object names.

## Required sign-off statement

The reviewer must state whether the pinned candidate is suitable for the claimed release stage,
list every unresolved finding by severity, identify untested boundaries, and confirm whether the
review evidence supports P-08. The release owner then links the signed report or approved internal
record, finding issues, retest evidence, and residual-risk decisions from the readiness matrix.
