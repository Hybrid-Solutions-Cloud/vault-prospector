# Product Requirements

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

### Discovery

- Enumerate accessible tenants where technically possible.
- Enumerate Azure subscriptions for each identity.
- Discover Azure Key Vault resources.
- Determine which identity or identities can access each vault.
- Record discovery failures without aborting unrelated discovery work.
- Support selective inclusion and exclusion of subscriptions and vaults.

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

## Non-functional requirements

- Local searches should normally complete in less than one second.
- Synchronization must be cancelable and resumable.
- Failures in one tenant must not block other tenants.
- The user interface must clearly distinguish online, offline, stale, and unauthorized states.
- Logs must exclude tokens and values.
- The application must support accessible keyboard navigation.
- The core domain and provider contracts must be testable without a UI.
- The storage format must support schema migration.
