# Release Readiness

This is the authoritative gate matrix for promoting Vault Prospector from an internal build to a
Windows Preview and later to General Availability (GA). A roadmap item being implemented does not
prove release readiness; every required gate needs current, reproducible evidence.

**Assessment date:** 2026-07-16

**Current decision:** **Not ready for public Preview promotion or GA**

**Current candidate:** `0.1.0-preview.2`

Status meanings:

- **Passed** — authoritative evidence exists and is linked or reproducible.
- **In progress** — concrete work or external validation is underway.
- **Blocked** — a required external or internal dependency currently prevents completion.
- **Not started** — required evidence does not yet exist.
- **GA only** — intentionally does not block Preview, but must pass before GA.

## Preview release gates

All rows marked **Required** must be **Passed** before a Preview go/no-go decision.

| ID | Gate | Required | Status | Current evidence or remaining work |
| --- | --- | --- | --- | --- |
| P-01 | Preview scope and explicit limitations | Yes | Passed | [Preview scope](release-scope.md) separates the Windows evaluation release from mobile, browser, provider, and enterprise backlog. |
| P-02 | Locked restore, formatting, warnings-as-errors build, and automated tests | Yes | Passed | Local Release verification on 2026-07-16: restore and formatting passed; build completed with 0 warnings and 0 errors; 23 tests passed. CI repeats these gates on `main`. |
| P-03 | Known dependency vulnerabilities and committed secrets | Yes | In progress | `dotnet list package --vulnerable --include-transitive` reported no vulnerable packages on 2026-07-16. Gitleaks runs in CI, but the current `main` CI result must be linked after GitHub service availability recovers. |
| P-04 | Secure-by-default metadata storage and offline cache | Yes | In progress | SQLCipher metadata storage fails closed; offline values use AES-GCM with DPAPI-protected keys and are disabled by default. Independent review and attack testing remain open. |
| P-05 | Mandatory user verification for reveal, copy, and cached-value access | Yes | In progress | Application-service enforcement was hardened in commit `21637c8`; callers can no longer disable verification for live retrieval. Independent Windows Hello behavior testing remains required. |
| P-06 | Authentication, token-cache isolation, MFA, Conditional Access, and identity removal | Yes | In progress | MSAL public-client interactive authentication and an app-specific cache exist. Live multi-tenant, guest, MFA, Conditional Access, reauthentication, and removal tests are not yet captured as release evidence. |
| P-07 | Read-only Azure behavior | Yes | Passed | Current provider discovers metadata and retrieves explicitly selected secret values. It contains no Key Vault mutation or Azure role-assignment operations. |
| P-08 | Independent security review with no unresolved critical/high findings | Yes | Not started | Threat model and unit tests exist, but review must be performed by someone independent of the implementation and findings must be tracked to closure. |
| P-09 | Clean-machine MSI install, upgrade, repair, uninstall, and retained-data behavior | Yes | In progress | Preview.2 evidence proves clean install, preview.1-to-preview.2 upgrade, and uninstall. Explicit MSI repair and documented rollback/recovery testing remain open. |
| P-10 | WinGet manifest validation and public repository acceptance | Yes | In progress | Local validation passes. PR [#403473](https://github.com/microsoft/winget-pkgs/pull/403473) was corrected to CRLF-only manifests and the MSI-proven ARP `DisplayVersion` on 2026-07-16; Microsoft revalidation and merge are pending. |
| P-11 | Chocolatey package validation and public repository acceptance | Yes | Blocked | The tested package hash matches the public release. Two submission attempts on 2026-07-16 returned Chocolatey HTTP 504 and the package is not visible; retry after repository recovery, then complete moderation. |
| P-12 | Immutable public artifacts, checksums, SBOM, and provenance | Yes | Passed | The public Preview.2 release contains immutable MSI/ZIP/NUPKG artifacts, SHA-256 checksums, SPDX SBOM, and keyless Sigstore bundles. |
| P-13 | Trusted Windows binary signing | Yes | Not started | Individual MSI and executable files are not Authenticode-signed. Obtain an appropriate code-signing identity, protect it, sign in CI, and verify reputation/timestamp behavior. |
| P-14 | First-run onboarding and actionable failure states | Yes | Not started | The current UI requires users to manually obtain and enter an Entra public-client application ID. A secure first-run flow and tested recovery paths are required. |
| P-15 | Accessibility and core-task usability | Yes | Not started | No keyboard-only, screen-reader, contrast, scaling, or structured usability evidence exists. Test identity setup, sync, search, reveal, copy, cache, purge, and exit. |
| P-16 | Privacy, telemetry, diagnostics, and data-retention disclosure | Yes | In progress | Telemetry is disabled and diagnostics are allow-listed/redacted. Publish a user-facing privacy/data-handling statement covering local files, token cache, clipboard, offline values, logs, uninstall retention, and deletion. |
| P-17 | Support, troubleshooting, vulnerability response, and rollback runbooks | Yes | In progress | User, authentication, release-verification, and security-reporting docs exist. Add operational support ownership, package rollback, credential rotation, failed-update recovery, and incident procedures. |
| P-18 | Preview go/no-go record | Yes | Not started | Requires every Preview gate above to pass, a named approver, release-candidate hashes, accepted residual risks, and a rollback decision. |

## GA promotion gates

GA requires every Preview gate to remain green plus the following evidence.

| ID | Gate | Status | GA evidence required |
| --- | --- | --- | --- |
| G-01 | Preview reliability and feedback cycle | Not started | Define consented feedback channels and success measures; triage all Preview feedback; close release-blocking defects; demonstrate stable upgrades across supported previews. |
| G-02 | Security assessment and vulnerability closure | Not started | Final independent assessment or penetration test, zero unresolved critical/high findings, accepted medium-risk exceptions with owners and dates, and a tested disclosure/response process. |
| G-03 | Data migration, backup, recovery, and device replacement | Not started | Test schema/key rotation, corrupted-state recovery, backup limitations, device migration, uninstall/reinstall, and supported upgrade paths without silent plaintext fallback or data loss. |
| G-04 | Production accessibility conformance | Not started | Complete a WCAG-aligned desktop accessibility assessment, remediate blocking findings, and publish supported assistive-technology behavior. |
| G-05 | Performance and scale | Not started | Define and pass startup, search, sync, memory, cancellation, and large-estate targets across representative identities, subscriptions, vaults, and object counts. |
| G-06 | Enterprise policy and administration | Not started | Deliver or explicitly defer with approved rationale: allowed tenants/providers, offline-cache and clipboard policy, managed configuration, audit-friendly diagnostics, and workload-identity boundaries. |
| G-07 | Repeatable signed release and update process | Not started | Produce a fresh release entirely from protected automation; prove MSI, WinGet, and Chocolatey update propagation; verify signatures, SBOM, provenance, hashes, rollback, and credential rotation. |
| G-08 | Operational readiness | Not started | Named support and security owners, severity/SLA definitions, incident and compromise runbooks, dependency and signing-key rotation, release monitoring, and end-of-support policy. |
| G-09 | Legal and privacy approval | Not started | Approve license notices, privacy statement, telemetry schema if enabled, data-retention language, third-party components, and store/package metadata. |
| G-10 | Formal GA go/no-go | Not started | Named approvers sign the completed matrix; all release artifacts and package-manager entries are independently installed and upgraded on clean supported Windows systems. |

## Evidence required for each release candidate

Every Preview refresh and GA candidate must record:

1. Source commit and immutable tag.
2. CI and release-workflow run URLs.
3. Exact artifact names, sizes, hashes, signatures, SBOMs, and provenance.
4. Test counts and results, vulnerability and secret-scan results, and coverage summary.
5. Clean-machine interactive and silent install, launch, repair, upgrade, uninstall, and rollback results.
6. Direct MSI, WinGet, and Chocolatey discovery/install/update results.
7. Authentication, sync, search, reveal, copy, offline-cache, purge, and identity-removal results.
8. Accessibility, performance, security-review, and known-defect results appropriate to the release stage.
9. Known limitations, residual risks, support ownership, and rollback triggers.
10. A dated go/no-go decision with named approver.

## Rollback triggers

Stop distribution or withdraw the affected package version when any of these occurs:

- a secret, token, private key, or decrypted cache value appears in logs, telemetry, diagnostics, or release artifacts;
- metadata or offline values are written without required encryption;
- reveal, copy, or cached-value access bypasses required user verification;
- an installer corrupts user state, cannot be cleanly removed, or breaks the supported upgrade path;
- an artifact hash or signature differs from the release evidence;
- a critical/high vulnerability affects a reachable release path without an effective mitigation;
- package-manager metadata installs an unexpected binary or version;
- authentication or authorization behavior grants access beyond the selected Azure identity's existing permissions.

The rollback action must preserve evidence, remove or deprecate affected public packages where
the repository permits it, publish a security/support notice, rotate compromised credentials,
and issue a new immutable version rather than replacing assets under an existing tag.
