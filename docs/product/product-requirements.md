# Product Requirements

These requirements describe the product direction. The implemented and deferred boundaries for the first public evaluation build are recorded in the [version 0.1 preview scope](release-scope.md); unmet requirements remain backlog items rather than implied release claims.

## Personas

### Multi-tenant cloud architect

Works across personal, employer, customer, lab, and community tenants. Needs fast discovery and search without constant portal and CLI context changes.

### Managed service provider engineer

Uses one or more management identities to access many customer tenants. Needs clear customer boundaries and confidence that the correct identity is being used.

### Developer

Needs occasional access to application secrets, connection strings, certificates, and API credentials. Wants favorites, project grouping, and secure copy behavior.

## Functional requirements

### Identity management

- Add multiple Microsoft Entra identities.
- Label identities with a friendly name.
- Show home tenant and discovered resource tenants.
- Identify expired, revoked, or interaction-required authentication contexts.
- Allow an identity to be disabled without deleting indexed metadata.
- Remove an identity and purge associated tokens.
- Support guest access and cross-tenant authorization.
- Keep app-owned authentication contexts independent from Azure CLI, Azure PowerShell, IDE, and terminal context files.
- Distinguish interactive Entra users, service principals, and managed identities as separate connection types.
- Offer managed identity authentication only when running on supported Azure compute.
- Prefer brokered interactive authentication for Windows users and credential-free workload authentication where supported.

### Discovery

- Enumerate accessible tenants where technically possible.
- Enumerate Azure subscriptions for each identity.
- Discover Azure Key Vault resources.
- Determine which identity or identities can access each vault.
- Record discovery failures without aborting unrelated discovery work.
- Support selective inclusion and exclusion of subscriptions and vaults.
- Show management-plane visibility and data-plane permissions separately for every discovered vault and connection.

### Indexing

- Index vault metadata.
- Index secret, key, and certificate metadata.
- Preserve object versions where useful.
- Store tags, enabled state, content type, creation date, update date, and expiration date when available.
- Track last successful sync time.
- Do not retrieve secret values during normal metadata indexing.
- Support incremental synchronization.

### Search

- Search object names.
- Search tags and descriptive metadata.
- Filter by workspace, identity, tenant, subscription, vault, object type, enabled state, expiration, and staleness.
- Show the precise source context for every result.
- Support favorites and recently accessed items.
- Provide deterministic sorting.

### Value retrieval

- Retrieve a secret value only after explicit user action.
- Request the minimum required Azure scope.
- Keep decrypted values in memory for the shortest practical period.
- Copy values through a protected clipboard workflow.
- Automatically clear clipboard data after a configurable duration where supported.
- Avoid displaying full values by default.
- Require local unlock for high-risk actions based on policy.

### Governed value mutation

- Operate read-only by default regardless of broader permissions already held by the connected identity.
- Define create or update capabilities separately for secrets, keys, and certificates.
- Require explicit administrator policy, capable Azure authorization, local verification, target confirmation, and an elevated-state indicator before any mutation.
- Record audit metadata without storing the value or private key material.
- Do not publicly release mutation capabilities before a separate threat model and independent security review are complete.

### Offline mode

- Metadata search must work offline.
- Secret values must not be available offline unless explicitly cached.
- Cached values must have configurable expiration.
- The user must be able to purge one item, one vault, one workspace, or the entire offline cache.
- Offline results must show staleness and last synchronization.
- Policy may disable value caching globally.

### Workspaces

- Create user-defined workspaces.
- Assign tenants, subscriptions, vaults, or identities to workspaces.
- Support a vault appearing in more than one workspace without duplicating its index.
- Allow workspace-specific display and offline-cache policy.

### Platform integration

- Use Windows Hello or equivalent platform verification on Windows.
- Use Keychain and Secure Enclave-backed capabilities where available on Apple platforms.
- Research Apple Password AutoFill and Windows credential-provider integration separately.
- Never imply integration exists where the operating system does not expose a suitable API.
- Support an optional Windows notification-area mode with an explicit locked background state.
- Research browser-extension, native-messaging, browser password-vault, and origin-bound autofill models before implementation.
- Require explicit mapping and user presence before a browser may receive a selected value.

### Provider ecosystem

- Add CyberArk as a planned provider with a provider-specific ADR, authentication model, permission mapping, and threat model.
- Preserve provider boundaries rather than reducing every source to a least-common-denominator permission model.

## Non-functional requirements

- Local searches should normally complete in less than one second.
- Synchronization must be cancelable and resumable.
- Failures in one tenant must not block other tenants.
- The user interface must clearly distinguish online, offline, stale, and unauthorized states.
- Logs must exclude tokens and values.
- The application must support accessible keyboard navigation.
- The core domain and provider contracts must be testable without a UI.
- The storage format must support schema migration.
