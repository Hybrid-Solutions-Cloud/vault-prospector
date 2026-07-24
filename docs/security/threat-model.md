# Threat Model

## Scope

This threat model covers the local application, Azure authentication, metadata indexing, value retrieval, offline caching, clipboard use, logs, diagnostics, plugins, and platform integration.

## Assets

- Microsoft Entra authentication tokens.
- Azure tenant and subscription relationships.
- Key Vault names and object metadata.
- Secret values.
- Key material and certificate private keys if future functionality permits access.
- Offline cached values.
- Local encryption keys.
- Search and access history.
- Workspace and customer associations.

## Adversaries

- Malware running as the same local user.
- A person with physical access to an unlocked device.
- A local administrator.
- A malicious or compromised plugin.
- A supply-chain attacker.
- A malicious dependency.
- An attacker intercepting network traffic.
- An authorized Azure user attempting to access data outside current permissions.
- Accidental disclosure through logs, screenshots, clipboard history, crash reports, or support bundles.

## Trust boundaries

1. User interface to application services.
2. Application to MSAL token cache.
3. Application to Azure control plane.
4. Application to Key Vault data plane.
5. Application to local metadata database.
6. Application to protected value store.
7. Application to operating-system clipboard.
8. Core application to plugins.
9. Application to diagnostics and telemetry.
10. Device boundary to backup and synchronization services.

## Principal threats and mitigations

### Token theft

Threat:

An attacker obtains access or refresh tokens from local storage or logs.

Mitigations:

- Use supported MSAL token-cache mechanisms.
- Protect cache material with platform APIs.
- Never log token content.
- Minimize scopes.
- Purge token entries when an identity is removed.
- Roll back a newly authenticated account if encrypted identity metadata cannot be persisted.
- Document limitations against same-user malware and local administrators.

### Workload credential confusion or stale revocation

Threat:

A workload profile inherits human/developer credentials, persists a projected token, rotates to an
unusable credential, or remains usable through stale in-memory state after revocation.

Mitigations:

- Use distinct credential implementations for managed identity, certificate, federation, and human
  interactive profiles.
- Never include Azure CLI, Azure PowerShell, IDE, terminal, or default developer credentials in a
  credential chain.
- Store certificate thumbprints or canonical token-file paths, never private keys, token content,
  or client secrets.
- Acquire an ARM token before first persistence or replacement-credential publication.
- Persist local revocation before removing provider state and re-read persisted profile state before
  every synchronization.
- Block online value retrieval for non-ready or disabled identities.
- Purge discovered-vault offline copies on local revocation and direct users to revoke externally
  owned credentials at their issuer.
- Allow non-destructive identity-scoped purge across active and retained removed-access mappings.
- Emit only allowlisted, redacted lifecycle fields.

### Offline cache extraction

Threat:

An attacker copies the local database or backup and decrypts cached values.

Mitigations:

- Separate metadata from value storage.
- Encrypt every cached payload.
- Authenticate expiry, fingerprint, item, vault, and workspace metadata before using it for release
  or scoped-purge decisions.
- Reject oversized protected-value envelopes and authenticated rotation records before JSON
  parsing.
- Protect the data-encryption key with platform-backed secure storage.
- Require local user verification before key release where supported.
- Bind protected keys to the device where practical.
- Support automatic expiry and immediate purge.
- Do not synchronize cached values through ordinary cloud file backup by default.

### Clipboard disclosure

Threat:

Copied values remain in clipboard history or are read by another process.

Mitigations:

- Use explicit copy actions.
- Automatically clear the clipboard after a short interval.
- Serialize clipboard leases so an older timer cannot clear a newer value.
- During an orderly exit, clear the clipboard only when it still contains the value copied by Vault Prospector.
- Warn that clipboard clearing cannot revoke data already read.
- Research Windows clipboard history and Apple Universal Clipboard behavior.
- Offer direct type or AutoFill integrations only after platform review.

### Shoulder surfing and screen capture

Threat:

A value is exposed through screen viewing, screenshots, screen recording, task switching, or app previews.

Mitigations:

- Mask values by default.
- Require deliberate reveal.
- Auto-hide after a short interval.
- Apply platform protections to app-switcher snapshots where available.
- Warn users when platform screen-capture prevention is incomplete.

### Stale foreground authorization across Windows lifecycle changes

Threat:

A revealed value or unlocked foreground session remains available after Windows is locked,
disconnected, suspended, resumed, logged on, logged off, or moved between console and remote
sessions.

Mitigations:

- Subscribe to Windows session-switch and power-mode events in the interactive desktop process.
- Treat every session transition plus suspend and resume as a security boundary.
- Immediately cancel active work, invalidate delayed sensitive presentation, mask any revealed
  value, close the in-app close prompt, and require foreground unlock again.
- Marshal operating-system event handling to the UI thread and detach the static Windows handlers
  during application shutdown.
- Do not treat ordinary battery or AC status changes as foreground-authorization boundaries.
- Validate the production event path on an installed candidate in an isolated interactive Windows
  environment before claiming the lifecycle gate.

### Authorization confusion

Threat:

The app retrieves a resource using the wrong identity, tenant, or customer context.

Mitigations:

- Store explicit access-path mappings.
- Display identity, tenant, subscription, and vault for every result.
- Never silently switch to a broader identity without showing the selected access path.
- Allow users to set preferred identities.
- Record non-sensitive retrieval context locally.
- Fail closed and dispose a decrypted value if retrieval-context persistence fails.

### Directory discovery overreach or token forwarding

Threat:

Workload discovery silently requests broad directory rights, uses a workload or terminal identity,
forwards a Graph bearer token to an untrusted page link, or implies that visibility proves
permission to use or manage a principal.

Mitigations:

- Require an enabled, ready interactive identity selected in Vault Prospector.
- Request delegated `Application.Read.All` only through a separate explicit user action; request no
  Graph write permission.
- Bind the interactive result to the selected MSAL home-account identifier.
- Acquire Graph tokens only from the app-owned MSAL cache and for the selected tenant.
- Accept pagination only from HTTPS `graph.microsoft.com`, disable automatic redirects, and enforce
  page/item limits.
- Require explicit selection of a discovered candidate and exact Key Vault before assessment.
- Use Azure caller permissions for the selected administrator and applicable role assignments,
  role definitions, deny assignments, and conditions for the candidate.
- Constrain every ARM request and next link to HTTPS `management.azure.com`, disable automatic
  redirects, and enforce page/item/role-definition limits.
- Apply scope inheritance, action exclusions, direct deny/exclusion semantics, and child-scope
  behavior. Treat conditional expressions, unreadable deny evidence, and possible group-deny
  membership as unproven rather than allowed.
- Detect Key Vault access-policy mode and refuse to reinterpret it as Azure RBAC.
- Label static RBAC evidence separately from runtime data-plane access; never acquire a candidate
  credential or retrieve a value during assessment.
- Keep every provisioning plan non-mutating and expose no execution command before the independent
  review gate.

### Stale offline values

Threat:

A cached value has been rotated or revoked in Azure but remains available offline.

Mitigations:

- Display cache age and last provider validation.
- Apply configurable expiration.
- Compare metadata fingerprints after synchronization.
- Invalidate cached values when the source version changes.
- Let enterprise policy prohibit offline value caching.

### Logging and crash disclosure

Threat:

Sensitive data enters logs, traces, exceptions, crash dumps, or support bundles.

Mitigations:

- Central redaction.
- Structured logging with allowlisted fields.
- Disable object serialization for sensitive types.
- Test that secret-bearing types cannot be logged.
- Generate diagnostics through an explicit sanitization pipeline.

### Malicious plugin

Threat:

A plugin reads data from unrelated providers or exfiltrates secrets.

Mitigations:

- Do not support arbitrary plugins in the initial release.
- Define narrow contracts.
- Require signed and allowlisted plugins if dynamic loading is introduced.
- Consider process isolation.
- Provide per-provider data access boundaries.
- Treat plugins as code execution, not as harmless extensions.

### Supply-chain compromise

Threat:

A dependency, build pipeline, or release artifact is compromised.

Mitigations:

- Lock dependencies.
- Enable dependency review and vulnerability scanning.
- Produce signed builds.
- Generate an SBOM.
- Protect release workflows.
- Use reproducible or attestable builds where practical.
- Restrict maintainer permissions.

## Residual risks

No local application can fully protect values after they are intentionally revealed to an already-compromised user session. Windows administrators, rooted or jailbroken devices, malware running as the same user, screen capture, and clipboard consumers remain important residual risks.

The product must communicate these limits honestly.
