# Vault Prospector

Vault Prospector is a local-first Windows desktop application for discovering and searching Azure Key Vault metadata across multiple Microsoft Entra identities, tenants, and subscriptions. Secret values are retrieved only after an explicit action and Windows Hello verification.

> **Release status:** `0.1.0` preview. Use non-production environments while evaluating the application and report security issues privately as described in [SECURITY.md](SECURITY.md).

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

Vault Prospector does not create Azure role assignments, rotate secrets, export keys or certificate private keys, share secrets, or send telemetry.

## Quick start

### Install a release

1. Download the Windows `win-x64` ZIP and its `.sha256` file from [GitHub Releases](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/releases).
2. Verify the checksum and Sigstore bundle by following [the release verification guide](docs/release.md).
3. Extract the ZIP to a user-writable folder.
4. Run `VaultProspector.App.exe`.
5. On the **Identities** tab, enter the client ID from your Microsoft Entra public-client app registration and select **Sign in interactively**.
6. Select the connected identity and choose **Sync selected**.

The required app registration takes about five minutes; see [Authentication setup](docs/authentication.md).

### Build from source

Prerequisites:

- PowerShell 7+
- .NET SDK 9.0.315 or a compatible 9.0 patch selected by `global.json`
- Windows for Windows Hello integration and the final release package

```powershell
pwsh ./scripts/Build.ps1
pwsh ./scripts/Package.ps1 -Version 0.1.0
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
- [Architecture](docs/architecture/architecture-overview.md)
- [Security requirements](docs/security/security-requirements.md)
- [Threat model](docs/security/threat-model.md)
- [Release and artifact verification](docs/release.md)
- [Product requirements](docs/product/product-requirements.md)
- [Preview release scope](docs/product/release-scope.md)
- [Roadmap](docs/product/roadmap.md)
- [Backlog](docs/product/backlog.md)
- [Contributing](CONTRIBUTING.md)

## Mobile status

Apple macOS/iOS and Google Android/Play applications are intentionally not part of this release. Their platform security, background execution, app-store distribution, and credential-provider work remain explicit deferred items in the roadmap and backlog.

## License

Vault Prospector is available under the [MIT License](LICENSE).
