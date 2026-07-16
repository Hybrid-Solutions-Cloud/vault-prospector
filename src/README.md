# Source Code

The production solution is split into six projects with inward-facing dependencies:

| Project | Responsibility |
|---|---|
| `VaultProspector.Domain` | Provider-neutral entities, search requests, cache policy, and disposable sensitive values. |
| `VaultProspector.Application` | Use cases and contracts for identity, discovery, search, retrieval, workspaces, storage, verification, clipboard, and diagnostics. |
| `VaultProspector.Infrastructure` | SQLCipher metadata repository, AES-GCM protected-value store, and redacted diagnostics. |
| `VaultProspector.Platform` | Windows DPAPI key protection and Windows Hello verification, with fail-closed non-Windows fallbacks. |
| `VaultProspector.Providers.Azure` | MSAL authentication, Azure Resource Manager discovery, and Azure Key Vault metadata/value access. |
| `VaultProspector.App` | Avalonia desktop composition, views, settings, and user interaction. |

The domain has no project dependencies. Application depends only on Domain; infrastructure, platform, provider, and app code implement or consume application contracts. Azure SDK types do not cross the provider boundary.

Build and test from the repository root:

```powershell
pwsh ./scripts/Build.ps1
```

See the [repository structure](../docs/architecture/repository-structure.md) and [architecture overview](../docs/architecture/architecture-overview.md) for details.
