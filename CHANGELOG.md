# Changelog

All notable changes to Vault Prospector are documented here. The project follows semantic versioning for published artifacts.

## [Unreleased]

## [0.3.0-preview.6] - 2026-07-27

### Fixed

- Restore readable Atlas text, selectors, browser-fill configuration, and independently scrolling
  Find Secrets results in the installed application.
- Unblock upgraded first-run profiles and restore connected-identity enable, removal, workload
  discovery, and synchronized context workflows.
- Complete browser-extension installation detection and guided setup.
- Restore the approved application and installer branding.

### Release

- Publish the unsigned manual-test Preview from source
  `8751df7f2a6c1014f3e51c4b570625364f9fb5f9`.
- Pass the exact-main and immutable-tag Windows build, tests, packaging, lifecycle, readiness,
  SBOM, Sigstore, and public checksum gates.

## [0.3.0-preview.5] - 2026-07-26

### Added

- Add trusted in-application release discovery, verified MSI download, and user-controlled
  installer handoff.
- Add privacy-safe diagnostics, external log collection, and redacted support-bundle export.
- Add policy-controlled reveal-verification grace, discovered tenant/subscription/vault selectors,
  minimize-to-notification-area behavior, and relevant service-principal filtering.
- Add guided browser-fill setup diagnostics and actionable isolated synchronization-error
  inspection with exact-scope retry.
- Add separately governed Azure Key Vault mutation operations behind default-deny release and
  machine-policy gates.

### Fixed

- Clear completed identity operations reliably so ready identities can synchronize without using
  Cancel as a workaround.
- Support policy-controlled current-account verification in Remote Desktop and AVD-equivalent
  sessions.
- Align the production Avalonia hierarchy with the approved C · Atlas design.

### Release

- Publish the unsigned manual-test Preview from source
  `1a4f9f7fdc470c71d5faad4aaa819c1452a15799`.
- Verify all 16 public assets and all five adjacent package checksums independently.

## [0.3.0-preview.3] - 2026-07-25

### Added

- Ship the product-owner-approved C · Atlas desktop hierarchy across installation, setup, daily
  use, administration, support, and settings workflows.
- Add policy-controlled current-account Windows credential verification for supported Remote
  Desktop and AVD sessions.
- Add in-app update review, privacy-safe support bundles, discovered-source selectors,
  notification-area lifecycle controls, and guided browser-fill setup.

### Fixed

- Keep startup fail-closed without opening a credential prompt until the user explicitly chooses
  **Verify and continue**.
- Replace the remaining legacy-derived locked surface with the persistent grouped Atlas shell.
- Preserve unreadable encrypted local data and require an explicit verified archive decision
  instead of silently rebuilding it.

## [0.2.0-preview.5] - 2026-07-25

### Fixed

- Preserve the bound identity-type collection while applying enterprise policy so a clean
  first-run profile selects `InteractiveUser` without a transient null conversion error.
- Add a regression assertion that the default selection remains valid without a collection reset.

## [0.2.0-preview.4] - 2026-07-25

### Fixed

- Use the HWND-bound `UserConsentVerifierInterop` API required for an unpackaged Windows desktop
  application instead of the UWP-only verification call.
- Identify Remote Desktop `DeviceNotPresent` results explicitly so the locked screen explains that
  repeated retries in the same remote session cannot open Windows verification.

## [0.2.0-preview.3] - 2026-07-25

### Changed

- Move CyberArk and native mobile delivery to separate future-roadmap releases and hide the
  unsupported CyberArk Windows surface by default.
- Replace Azure DevOps build definitions with GitHub Actions on the governed HCS Linux runner and
  repeatable ephemeral Azure Windows fallback.
- Add reproducible MSIX packaging and validation for the future free Microsoft Store–signed
  distribution path while keeping direct Preview downloads explicitly unsigned.
- Remove arbitrary evaluator-count and waiting-period quotas from GA promotion; retain
  evidence-based workflow coverage, defect disposition, exact-candidate validation, and named
  approval.
- Isolate first-process .NET, SQLCipher, and cryptographic activation from the repository
  initialization performance metric without changing its two-second limit.
- Repair the one-shot Windows release environment so pinned Cosign tooling, Sigstore provenance,
  and GitHub App publication run from a clean Tier-4 machine.

## [0.2.0-preview.1] - 2026-07-24

### Added

- Add an operational-readiness contract and validator, weekly dependency update coverage, scheduled
  vulnerability/runtime/public-endpoint monitoring, and a published support/end-of-support policy.
- Add fail-closed local unlock/recovery, schema-v4 migration, and an internal crash-recoverable
  SQLCipher/offline-value key-rotation engine.
- Add a guided first-run path that opens directly on identity setup after local unlock, separates
  Windows verification from Microsoft authentication and metadata sync, and uses
  authentication-specific connection actions.
- Add a Settings inventory for app-generated recovery archives and explicit per-archive permanent
  deletion requiring `DELETE ARCHIVE`, fresh Windows verification, containment checks, and no
  pending rotation recovery.
- Add certificate, federated, and detected-host managed-identity connection profiles with isolated
  credentials, validate-before-persist replacement, local revocation, and cache purge.
- Add explicit-account managed-identity and consented Microsoft Graph service-principal discovery,
  permission distinctions, and non-mutating provisioning previews.
- Add an exact-scope, read-only workload authorization assessment covering caller permissions,
  inherited/transitive role grants, exclusions, deny assignments, and conditions without
  impersonating the candidate or retrieving Key Vault data.
- Add per-identity subscription/vault discovery scope, complete workspace resource assignment,
  workspace cache/clipboard policy, and reconciliation of removed provider objects.
- Add explicit notification-area close behavior, immediate lock-on-hide, safe tray status, and
  opt-in metadata-only background synchronization.
- Add fail-safe foreground locking for every Windows session transition and for suspend/resume,
  including active-operation cancellation and sensitive-presentation invalidation.
- Add explicit identity-scoped offline-value purge, including historical removed access paths.
- Add comparative desktop UI research and four interactive setup/search/reveal/settings concepts;
  production selection remains gated on representative-user and assistive-technology evidence.
- Add a Preview Chromium/Firefox browser-fill implementation with toolbar-only activation,
  exact origin/frame/purpose mappings, authenticated native messaging, protected fail-closed
  machine policy, one-time desktop confirmation, fresh Windows verification, and value-free audit.
- Add a Preview CyberArk Privilege Cloud provider with explicit service-user profiles, safes,
  accounts, versions, direct safe-member evidence, SQLCipher schema v6 metadata, DPAPI-isolated
  credentials, bounded metadata sync, fresh-verified reveal/copy, fail-closed local revocation,
  explicit removal, and value-free audit.
- Add iOS and Android source prototypes with a shared fail-closed search/retrieval workflow,
  platform-native protected storage and verification hosts, lifecycle/clipboard/capture controls,
  locked builds, and package-disabled native autofill feasibility extensions.
- Add a CI-enforced 50,000-object performance probe covering encrypted initialization/reopen,
  metadata sync, search, cancellation, memory, and storage targets.
- Add a deterministic NuGet/npm component inventory and generated third-party notice, legal/privacy
  CI drift checks, package/store metadata and open-review records, and product license, privacy,
  and notice files in Windows distributable payloads.
- Add Preview versioned HKLM enterprise policy with packaged ADMX/ADML templates, allowed
  tenant/provider/identity-type controls, clipboard and offline-cache restrictions, service-layer
  enforcement, safe Settings status, and deterministic fail-closed package validation.

### Changed

- Batch encrypted metadata upserts, derive SQLCipher's compatible effective key once per repository
  lifetime without connection pooling, and select preferred search access paths deterministically.
- Migrate the complete Windows desktop solution, tests, locked dependency graphs, self-contained
  packaging, and protected CI/release automation from .NET 9 to .NET 10 LTS.

### Security

- Retry authenticated rotation-journal replacement only for bounded transient Windows I/O/access
  failures; persistent filesystem or ACL failures continue to stop fail-closed.
- Complete revocation cleanup after the profile is durably revoked, even when provider credential
  removal fails, and report any residual offline-value purge failure.
- Replace persisted authentication exception text with a fixed safe interaction-required message.
- Bound Microsoft Graph, ARM, local envelope, rotation-record, and settings JSON before parsing;
  require default-port HTTPS Microsoft Graph pagination.
- Retry transient Windows directory swaps during rotation recovery without allowing cancellation
  to strand the canonical data path after the active state has moved.

### Fixed

- Embed the product icon in the MSI and bind the advertised Start-menu shortcut to that icon at
  index 0, preventing the installed shortcut from falling back to a blank document icon.
- Keep full-history secret scanning strict while constraining one historical synthetic
  certificate-thumbprint exception by exact value, file, and commit.

## [0.1.1-preview.1] - 2026-07-17

### Added

- Retain each successful `main` CI run's commit-addressed unsigned Windows candidate for 14 days, including MSI, package-manager artifacts, checksums, and machine-readable source/run provenance for clean-machine validation.
- Add HCS-governed public Preview intake, an explicit publication notice, a private security route, business-day triage, and measurable Preview-to-GA feedback criteria without enabling telemetry.
- Add a fail-closed, structured Windows Installer lifecycle scenario covering published checksums, install, major upgrade, forced repair, downgrade rejection, uninstall cleanup, and retained user state.
- Provide a default multi-tenant Vault Prospector public-client registration, a guided first-identity experience, and an advanced organization-controlled registration option.
- Show redacted, actionable recovery guidance for authentication, authorization, Windows verification, protected-data integrity, policy, and damaged-settings failures.

### Changed

- Permit only explicitly versioned unsigned Preview evaluation tags through the protected release
  workflow when Artifact Signing is unavailable; stable and GA tags remain fail-closed.
- Authenticate encrypted-cache descriptors before applying expiry, fingerprint, or scoped-purge decisions; reject and remove malformed, substituted, or tampered entries without trusting their claimed scope.
- Roll back newly authenticated MSAL accounts when encrypted identity persistence fails, audit offline-secret opens with fail-closed disposal, and track clipboard ownership with a zeroized digest instead of retaining a second plaintext copy.
- Restore NVDA focus events throughout selected secondary tabs, announce complete safe actionable errors through a focused return control, and sequence polite status announcements before operation focus restoration.
- Restore keyboard focus to the initiating control after external Entra, Windows Hello, or other asynchronous operation surfaces close, while rejecting controls that became unavailable.
- Raise numeric stepper controls so their rendered increment and decrement targets meet the WCAG 2.2 AA 24-pixel minimum at default Windows scaling.
- Honor the Windows 100–225% text-size preference through centralized font resources, choose the stacked layout from effective text-scaled width, and wrap the product title so all tabs and task boundaries remain reachable at 200% text-only scaling.
- Follow Windows High Contrast changes at runtime and use system theme resources for readable text-entry placeholders and keyboard-focused selectors.
- Fit the window to the scaled Windows work area and stack task panels below 720 logical pixels so Search, Identities, Workspaces, Settings, and About remain reachable at 200% display scaling.
- Render first-run guidance with its verified white foreground instead of inheriting unreadable dark text on the dark green panel.
- Give every text-entry, selector, list, and numeric control an explicit UI Automation name, and expose application status changes as a polite live region for assistive technology.
- Reduce and center the initial Windows viewport, and make selected-object actions vertically scrollable so every action remains reachable in a 1024-by-768 work area.
- Disable identity synchronization and removal until an identity is selected, removing unavailable actions from first-run keyboard navigation.
- Disable result, secret, cache, workspace, filter, and general operation controls unless their exact selection, object-type, policy, input, and busy-state prerequisites are satisfied.
- Clear stale result selections after search and reconcile identity selection after refresh or removal.

### Security

- Refuse to mint replacement DPAPI keys when an existing encrypted database or offline-value
  envelope has lost its matching key, preserving the encrypted state for explicit recovery.
- Reject corrupted SQLCipher databases, incomplete current schemas, invalid foreign-key
  relationships, wrong keys, and future schema versions without silently rebuilding or
  downgrading protected local data.
- Reject non-canonical DPAPI key purposes instead of allowing distinct purposes to collapse onto one key path.
- Publish encrypted offline-cache replacements atomically and validate their expiration and source fingerprint before writing.
- Dispose retrieved secret material if access-history persistence fails, and reject non-secret metadata before cached-value verification or access.
- Add best-effort finalizer zeroization for undisposed sensitive values.
- Serialize clipboard leases, prevent stale timers from clearing newer copies, and clear an unchanged app-owned value during orderly exit.
- Authenticate offline-cache expiration, source fingerprint, vault, workspace, and descriptor metadata with AES-GCM associated data.
- Invalidate legacy preview cache envelopes whose descriptor metadata was not authenticated; users must explicitly cache those values again.
- Require application-boundary Windows Hello verification for live retrieval, copy, offline caching, and cached retrieval.
- Validate Entra application client IDs before constructing app-specific MSAL cache paths.
- Request Azure Key Vault delegated consent during interactive sign-in while continuing to acquire separate Resource Manager and Key Vault audience tokens.

## [0.1.0-preview.2] - 2026-07-16

### Added

- Per-machine Windows x64 MSI with Start menu and Installed apps integration.
- Validated WinGet manifests and a Chocolatey package generated from the release MSI.
- Public binary-distribution and package-manager submission automation.
- iPhone/iOS and Android/Google Play applications marked as coming soon in the product backlog.

## [0.1.0-preview.1] - 2026-07-16

### Added

- Avalonia Windows desktop application with a Vault Prospector product identity.
- Multi-account Microsoft Entra interactive authentication through MSAL.
- Azure subscription and Key Vault discovery with version-aware secret, key, and certificate metadata indexing.
- SQLCipher-encrypted metadata storage, deterministic offline search, filters, favorites, recent access, and workspaces.
- Explicit Windows Hello-gated secret reveal/copy with timed clipboard clearing.
- Opt-in AES-GCM offline values protected by DPAPI, expiration, source-fingerprint invalidation, and multi-scope purge.
- Redacted diagnostics, cancelable/partial synchronization, automated tests, CI security analysis, and reproducible Windows packaging.
- Deferred Apple/iOS and Google/Android delivery plan.

[0.3.0-preview.6]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.6
[0.3.0-preview.5]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.5
[0.3.0-preview.3]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.3
[0.2.0-preview.5]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.5
[0.2.0-preview.4]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.4
[0.2.0-preview.3]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.3
[0.2.0-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.1
[0.1.1-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.1.1-preview.1
[0.1.0-preview.2]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.2
[0.1.0-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.1
