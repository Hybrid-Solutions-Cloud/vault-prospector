# Changelog

All notable changes to Vault Prospector are documented here. The project follows semantic versioning for published artifacts.

## [Unreleased]

### Added

- Add HCS-governed public Preview intake, an explicit publication notice, a private security route, business-day triage, and measurable Preview-to-GA feedback criteria without enabling telemetry.
- Add a fail-closed, structured Windows Installer lifecycle scenario covering published checksums, install, major upgrade, forced repair, downgrade rejection, uninstall cleanup, and retained user state.
- Provide a default multi-tenant Vault Prospector public-client registration, a guided first-identity experience, and an advanced organization-controlled registration option.
- Show redacted, actionable recovery guidance for authentication, authorization, Windows verification, protected-data integrity, policy, and damaged-settings failures.

### Changed

- Give every text-entry, selector, list, and numeric control an explicit UI Automation name, and expose application status changes as a polite live region for assistive technology.
- Reduce and center the initial Windows viewport, and make selected-object actions vertically scrollable so every action remains reachable in a 1024-by-768 work area.
- Disable identity synchronization and removal until an identity is selected, removing unavailable actions from first-run keyboard navigation.
- Disable result, secret, cache, workspace, filter, and general operation controls unless their exact selection, object-type, policy, input, and busy-state prerequisites are satisfied.
- Clear stale result selections after search and reconcile identity selection after refresh or removal.

### Security

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

[0.1.0-preview.2]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.2
[0.1.0-preview.1]: https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases/tag/v0.1.0-preview.1
