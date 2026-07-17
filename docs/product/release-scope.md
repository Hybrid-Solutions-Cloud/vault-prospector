# Version 0.1 Preview Scope

Version [`0.1.1-preview.1`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.1.1-preview.1)
is the current unsigned Windows desktop Preview for non-production evaluation. It delivers the
local-first Azure Key Vault workflow through encrypted metadata search, multi-identity discovery,
explicit Windows Hello-gated secret retrieval, protected clipboard handling, optional expiring
offline values, and workspaces. `0.1.0-preview.2` is withdrawn and must not be installed or
resubmitted to package managers; `0.1.0-ci.68` is superseded by this Preview.

## Included

- Windows x64 MSI installer and portable self-contained ZIP.
- Generated, validated WinGet manifests and a Chocolatey package for community-repository submission.
- MSAL public-client interactive authentication with no client secret.
- Multiple Microsoft Entra accounts and subscription/vault discovery.
- Version-aware secret, key, and certificate metadata indexing.
- SQLCipher-encrypted local search with source context and filters.
- Explicit secret reveal/copy and optional AES-GCM offline cache protected by DPAPI and Windows Hello.
- Favorites, recent-access ordering, workspace scoping, cancelable sync, and isolated Azure errors.
- CI build/test, warnings-as-errors .NET security analysis, dependency vulnerability and secret scanning, SBOM, and a keyless Sigstore bundle.

## Preview limitations tracked after 0.1

- Subscription and vault inclusion/exclusion is not yet configurable before discovery.
- Identity disablement and explicit reauthentication controls are not yet exposed in the UI.
- Workspace assignment currently supports identities and vaults; direct tenant/subscription assignment and per-workspace policy editing remain backlog items.
- Encrypted schema migration is covered from the internal version 1 shape to version 2. Future,
  corrupt, wrong-key, or incomplete current databases fail closed without silent repair, and a
  missing protected key does not replace recoverable encrypted state. Migration from every
  actually published schema, application-managed backup/key rotation, and cross-device recovery
  remain GA work.
- The SQLCipher native bundle currently reports a NuGet deprecation/legacy-package advisory without a published replacement; the release gate separately confirms that no known vulnerable packages are present.
- Azure end-to-end behavior depends on the evaluator's tenant consent policy, Conditional Access policy, and RBAC/data-plane permissions. The default product registration is currently not publisher-verified, so some tenants require administrator approval; an organization-controlled public-client registration remains available. Automated tests use provider contracts and do not contain a live tenant credential.
- Individual binaries are not Authenticode-signed. Release archives have checksums, keyless Sigstore bundles, and SBOMs. GitHub-native artifact attestations are unavailable for this private repository under the organization's current plan; the workflow enables them automatically if the repository becomes public.
- iPhone/iOS and Android/Google Play applications are coming soon as listed in the [roadmap](roadmap.md) and [backlog](backlog.md); they are not part of this Windows release.

Evaluate this preview in non-production environments. Azure remains the source of truth, and no release claim expands a user's existing Azure authorization.
