# Changelog

All notable changes to Vault Prospector are documented here. The project follows semantic versioning for published artifacts.

## [Unreleased]

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

[0.1.1-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.1.1-preview.1
[0.1.0-preview.2]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.2
[0.1.0-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.1
