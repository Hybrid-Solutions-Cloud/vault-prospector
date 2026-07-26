# Vault Prospector

Vault Prospector is a local-first Windows desktop application for discovering and searching Azure Key Vault metadata across multiple Microsoft Entra identities, tenants, and subscriptions. Secret values are retrieved only after an explicit action and Windows Hello verification.

> **Release status:** [`0.3.0-preview.5`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.5) is the current unsigned Windows Preview for non-production evaluation. Windows displays **Unknown Publisher**; verify the published SHA-256 before installation. Submit voluntary, non-sensitive feedback through the [public Preview feedback process](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/blob/main/FEEDBACK.md), and report security issues privately as described in [SECURITY.md](SECURITY.md).

## What works

- Interactive Microsoft Entra sign-in with MFA and Conditional Access support.
- Multiple connected identities with isolated token-cache entries.
- Subscription and Azure Key Vault discovery.
- Secret, key, and certificate metadata indexing without retrieving values.
- SQLCipher-encrypted local metadata storage and offline search.
- Deterministic search by name and tags, with workspace, identity, tenant, subscription, vault, type, enabled, expiration, favorite, staleness, and recent-access controls.
- Explicit secret retrieval, masked display, Windows Hello verification, and timed clipboard clearing.
- Optional AES-GCM encrypted offline values, disabled by default and protected with Windows DPAPI.
- Version-aware indexing, workspaces, favorites, access recency, cancelable synchronization, partial-sync diagnostics, and per-vault error isolation.
- Redacted local diagnostics with no tokens, secret values, usernames, vault names, or object names.
- Preview machine-managed enterprise policy for allowed tenants, providers, and identity types,
  clipboard/offline-cache restrictions, packaged ADMX/ADML templates, and fail-closed enforcement.
- Preview, fail-closed browser-fill source with explicit origin/field mappings, protected machine
  policy, one-time desktop confirmation, and fresh Windows verification.

CyberArk and native mobile implementations remain unsupported future-roadmap source. They are not
part of the current Windows release or its GA gate.

Vault Prospector does not create Azure role assignments, rotate secrets, export keys or certificate private keys, share secrets, or send telemetry.

## Quick start

### Install a release

1. Download the Windows x64 MSI and its `.sha256` file from the [public distribution releases](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases).
2. Verify the checksum and Sigstore bundle by following [the release verification guide](docs/release.md).
3. Run the MSI. Direct packages are currently unsigned and display **Unknown Publisher**. The
   future free trusted channel is the Microsoft Store–signed MSIX.
4. Open **Vault Prospector** from Start.
5. On the **Identities** tab, keep the recommended Vault Prospector registration and select **Continue to Microsoft sign-in**. Use a custom public-client registration only when your organization requires one.
6. Select the connected identity and choose **Sync selected**.

The portable `win-x64` ZIP remains available for users who cannot run an installer. WinGet and Chocolatey commands will be enabled after their community repositories approve the package; see [Windows package distribution](docs/package-distribution.md).

Tenant consent and optional custom-registration requirements are explained in [Authentication setup](docs/authentication.md).

### Build from source

Prerequisites:

- PowerShell 7+
- .NET SDK 10.0.302 or a compatible 10.0 patch selected by `global.json`
- Windows for Windows Hello integration and the final release package

```powershell
pwsh ./scripts/Build.ps1
pwsh ./scripts/PackageInstaller.ps1 -Version 0.3.0-preview.5
pwsh ./scripts/PackageDistribution.ps1 -Version 0.3.0-preview.5
```

HCS Tier 1 WSL is supported for restore, formatting, build, and non-Windows tests. The protected desktop release is built on a Windows runner so the Windows Hello projection is included.

## Security defaults

- Azure remains the source of truth.
- Metadata sync never requests secret values.
- The metadata database is encrypted; the app refuses to create a plaintext fallback.
- Offline values are stored separately and remain disabled until explicitly enabled.
- Windows Hello is required for reveal, copy, and offline caching.
- Clipboard content is cleared only if it still matches the value copied by Vault Prospector.
- Telemetry is disabled.

No local application can protect a deliberately revealed value from malware already running as the same user or from a local administrator. Review the [threat model](docs/security/threat-model.md) before approving production use.

## Documentation

- [User guide](docs/user-guide.md)
- [Authentication setup](docs/authentication.md)
- [Machine-managed enterprise policy](docs/enterprise-policy.md)
- [Architecture](docs/architecture/architecture-overview.md)
- [Security requirements](docs/security/security-requirements.md)
- [Threat model](docs/security/threat-model.md)
- [Privacy and local data handling](docs/privacy.md)
- [Browser integration and administrator policy](docs/browser-integration.md)
- [CyberArk Privilege Cloud integration](docs/cyberark-integration.md)
- [Mobile applications](docs/mobile-applications.md)
- [CI build environments](docs/ci-build-environments.md)
- [Preview feedback and GA promotion](docs/product/preview-feedback.md)
- [Release and artifact verification](docs/release.md)
- [Release operations and incident runbook](docs/release-operations-runbook.md)
- [Windows package distribution](docs/package-distribution.md)
- [Product requirements](docs/product/product-requirements.md)
- [Preview release scope](docs/product/release-scope.md)
- [Preview and GA release readiness](docs/product/release-readiness.md)
- [Roadmap](docs/product/roadmap.md)
- [Backlog](docs/product/backlog.md)
- [Contributing](CONTRIBUTING.md)

## Mobile status

iPhone/iOS and Android applications now have an unreleased shared UI and native security hosts.
They remain **coming soon** and are not included in the current Windows preview: exact-commit CI,
physical-device security/accessibility validation, protected signing, TestFlight/Play testing, and
store review are still required.

## License

Vault Prospector is available under the [MIT License](LICENSE).
