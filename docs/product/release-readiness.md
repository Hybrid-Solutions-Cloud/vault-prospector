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
| P-02 | Locked restore, formatting, warnings-as-errors build, and automated tests | Yes | Passed | Local Release verification on 2026-07-16: locked restore, structured vulnerability inspection, and formatting passed; build completed with 0 warnings and 0 errors; 44 tests passed. CI repeats these gates on `main`. |
| P-03 | Known dependency vulnerabilities and committed secrets | Yes | Passed | CI run [29541552073](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/29541552073) enforced the structured direct/transitive NuGet vulnerability gate and scanned full Git history with pinned Gitleaks; both jobs passed. Run [29541719343](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/29541719343) repeated the result on the authenticated-cache change. |
| P-04 | Secure-by-default metadata storage and offline cache | Yes | In progress | SQLCipher metadata storage fails closed; offline values use AES-GCM with DPAPI-protected keys and are disabled by default. DPAPI keys publish atomically under concurrency. Envelope v2 authenticates expiration, fingerprint, and scope metadata; regression tests prove descriptor tampering fails and legacy unauthenticated envelopes are invalidated. Independent review and attack testing remain open. |
| P-05 | Mandatory user verification for reveal, copy, and cached-value access | Yes | In progress | Application-service enforcement prevents callers from bypassing verification for live retrieval, copy, offline caching, or cached retrieval; regression tests cover each boundary. Independent Windows Hello behavior testing remains required. |
| P-06 | Authentication, token-cache isolation, MFA, Conditional Access, and identity removal | Yes | In progress | The default multi-tenant product registration and home service principal are active with no client secret or certificate. MSAL uses separate app-owned caches, validates client IDs before cache-path construction, and requests ARM plus Key Vault delegated consent. Live multi-tenant, guest, MFA, Conditional Access, tenant-consent, reauthentication, and removal tests are not yet captured as release evidence. See [onboarding and registration evidence](../release-evidence/onboarding-registration-2026-07-16.md). |
| P-07 | Read-only Azure behavior | Yes | Passed | Current provider discovers metadata and retrieves explicitly selected secret values. It contains no Key Vault mutation or Azure role-assignment operations. |
| P-08 | Independent security review with no unresolved critical/high findings | Yes | Not started | Threat model and unit tests exist, but review must be performed by someone independent of the implementation and findings must be tracked to closure. |
| P-09 | Clean-machine MSI install, upgrade, repair, uninstall, and retained-data behavior | Yes | In progress | Preview.2 evidence proves clean install, preview.1-to-preview.2 upgrade, and uninstall. A repeatable 21-gate Windows scenario additionally passed hash verification, forced repair with file restoration, downgrade rejection/current-version preservation, cleanup, and retained-state behavior on 2026-07-16. A negative test proved it refuses and preserves a pre-existing installation; it emits structured evidence. The same harness must still pass independently on a clean supported VM against the final signed candidate; application-level failed-update recovery also remains. |
| P-10 | WinGet manifest validation and public repository acceptance | Yes | In progress | Local validation passes. PR [#403473](https://github.com/microsoft/winget-pkgs/pull/403473) was corrected to CRLF-only manifests and the MSI-proven ARP `DisplayVersion` on 2026-07-16; Microsoft revalidation and merge are pending. |
| P-11 | Chocolatey package validation and public repository acceptance | Yes | Blocked | The tested package hash matches the public release. Two submission attempts on 2026-07-16 returned Chocolatey HTTP 504 and the package is not visible; retry after repository recovery, then complete moderation. |
| P-12 | Immutable public artifacts, checksums, SBOM, and provenance | Yes | Passed | The public Preview.2 release contains immutable MSI/ZIP/NUPKG artifacts, SHA-256 checksums, SPDX SBOM, and keyless Sigstore bundles. |
| P-13 | Trusted Windows binary signing | Yes | Blocked | The release workflow now fails closed and is prepared to Authenticode-sign app binaries and MSI through OIDC-based Azure Artifact Signing, then regenerate and verify signed hashes. The HCS subscription has no signing account; an owner must complete the portal-only Public Trust [identity and profile setup](../artifact-signing.md), after which a fresh candidate must prove clean-machine trust and timestamp behavior. |
| P-14 | First-run onboarding and actionable failure states | Yes | In progress | First run now selects the product registration, guides the first identity connection, supports an advanced organization-controlled registration, migrates old settings, and displays redacted recovery actions. The Release UI rendered correctly in a Windows smoke run and exposes named automation controls; automated tests cover defaults, migration, malformed settings, secret-safe errors, Windows verification, and damaged settings. Complete keyboard/screen-reader usability, startup timing, and live recovery scenarios remain. See [onboarding and registration evidence](../release-evidence/onboarding-registration-2026-07-16.md). |
| P-15 | Accessibility and core-task usability | Yes | In progress | A preliminary first-run audit confirmed keyboard access to the dynamic custom-registration field, meaningful automation names, and passing contrast for the new onboarding/error colors. Command-state rules and tests now cover identity, result type, cache policy, workspace prerequisites, empty input, filters, and busy state. A broader Windows runtime retest, target sizes, 200% scaling, high contrast, NVDA/Narrator, and structured usability testing remain. See the [preliminary accessibility audit](../release-evidence/accessibility-audit-2026-07-16.md). |
| P-16 | Privacy, telemetry, diagnostics, and data-retention disclosure | Yes | Passed | [Privacy and local data handling](../privacy.md) documents local files, tokens, Azure/network activity, clipboard, offline values, diagnostics, retention, migration, and deletion. Project-controlled telemetry is disabled. |
| P-17 | Support, troubleshooting, vulnerability response, and rollback runbooks | Yes | Passed | The [release operations and incident runbook](../release-operations-runbook.md) defines ownership, publication, verification, 5xx handling, failed-update recovery, withdrawal, severity/incident response, and credential rotation. The security policy provides a private HCS contact and response targets. |
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
