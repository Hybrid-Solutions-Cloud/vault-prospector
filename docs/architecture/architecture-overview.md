# Architecture Overview

## Architectural style

Vault Prospector is a local-first, modular application with explicit boundaries between:

- User interface.
- Application orchestration.
- Core domain.
- Provider integrations.
- Local persistence.
- Authentication.
- Platform security capabilities.
- Optional plugins.

The implementation uses a Clean Architecture-inspired dependency direction. This does not require every enterprise pattern; the goal is to protect the domain and prevent UI, Azure SDK, database, or operating-system concerns from becoming inseparable. The [preview scope](../product/release-scope.md) distinguishes delivered behavior from target architecture that remains on the backlog.

## Major components

### Application shell

Responsibilities:

- Navigation.
- Search experience.
- Identity and workspace management.
- Sync status.
- Settings.
- Local unlock requests.
- Accessible desktop interaction.

### Application layer

Responsibilities:

- Use-case orchestration.
- Synchronization workflows.
- Search requests.
- Retrieval and clipboard workflows.
- Policy evaluation.
- Error translation.
- Background task coordination.

### Domain layer

Responsibilities:

- Identity descriptors.
- Tenant, subscription, vault, and object models.
- Workspace associations.
- Index records.
- Cache policy.
- Staleness.
- Access-path selection.
- Security invariants.

The domain must not depend on Avalonia, Azure SDKs, SQLite, MSAL, or operating-system APIs.

### Azure provider

Responsibilities:

- Authentication integration.
- Tenant and subscription enumeration.
- Azure Resource Graph or ARM resource discovery.
- Key Vault data-plane metadata enumeration.
- Secret, key, and certificate retrieval.
- Azure-specific error mapping.
- Throttling and retry behavior.

### CyberArk Privilege Cloud provider

Responsibilities:

- Dedicated CyberArk Identity service-user authentication with operation-scoped tokens.
- Privilege Cloud safe, account, direct permission, and version metadata enumeration.
- Explicit account/version value retrieval only after application authorization.
- CyberArk-specific response, pagination, endpoint, status, and size validation.
- Provider-specific error categories without response-body disclosure.

CyberArk models are not normalized into Azure identities, subscriptions, vaults, RBAC, or object
types. SQLCipher schema v7 preserves the schema-v6 CyberArk boundary and adds the hash-chained,
value-free governed Azure mutation audit.
The service-user client credential is stored separately in a per-profile DPAPI file.

### Local index

Responsibilities:

- Persist searchable metadata.
- Maintain source and version relationships.
- Support fast filtering and full-text or prefix search.
- Track synchronization checkpoints.
- Support schema migration.

### Protected value store

Responsibilities:

- Store only explicitly cached values.
- Encrypt values with a data-encryption key.
- Protect the data-encryption key using platform-backed secure storage.
- Enforce item expiration.
- Purge values securely within platform limitations.
- Remain logically and physically distinct from the metadata index where practical.

### Platform security adapter

Responsibilities:

- Windows Hello integration.
- Read-only Windows machine-policy access.
- Apple Keychain and LocalAuthentication integration.
- Secure random generation.
- Data protection.
- Clipboard control.
- Screen-capture and background-state mitigations where available.

Machine policy is parsed into an immutable application-layer snapshot. The UI may explain its safe
status, but provider, identity, discovery, value, clipboard, and offline-cache services enforce it
independently so a caller cannot bypass the boundary through view-model state.

### Plugin host

Responsibilities:

- Load trusted provider extensions.
- Expose narrow provider contracts.
- Enforce version compatibility.
- Prevent plugins from receiving unrelated provider data.

Dynamic third-party plugins may be postponed until a safe trust and signing model is defined.

## Data flow: metadata synchronization

1. The user selects an identity, workspace, or global sync.
2. The application reads machine policy and rejects a disallowed provider, identity type, or home
   tenant before obtaining a token.
3. The application obtains a token through the authentication adapter.
4. The Azure provider receives the allowed-tenant constraint before discovering subscriptions and
   vaults.
5. The provider enumerates object metadata without requesting values.
6. The application reapplies tenant constraints before the index transaction.
7. Records are normalized into domain index models.
8. The preview index transaction applies additions and updates. Tombstone reconciliation and
   durable checkpoints remain post-preview backlog work.
9. Search becomes immediately available against the local index.
10. Sync errors are retained as non-sensitive diagnostics.

## Data flow: secret retrieval

1. The user selects a result.
2. The application resolves a valid identity-to-vault access path.
3. Machine, user, and workspace policy must all permit the provider, tenant, identity type, and
   requested clipboard/offline action.
4. Policy determines whether local unlock is required.
5. The Azure provider requests the selected version.
6. The value exists in memory only.
7. The user may reveal or copy it.
8. The UI clears displayed content and application buffers as soon as practical.
9. The value is not persisted unless the user explicitly enables offline caching.

## Data flow: CyberArk metadata synchronization

1. The user selects an enabled CyberArk profile and explicitly starts sync.
2. The application unprotects that profile's DPAPI credential for the operation.
3. The CyberArk provider obtains an operation-scoped Identity/platform token.
4. The provider lists visible safes, direct service-user member evidence, accounts, and versions
   through bounded same-origin Privilege Cloud endpoints.
5. SQLCipher atomically replaces only that profile's CyberArk metadata.
6. The credential and tokens are disposed/scoped without entering SQLCipher or diagnostics.
7. No password-retrieval endpoint participates in this flow.

## Data flow: CyberArk value retrieval

1. The user selects the exact profile, account, optional version, action, and non-sensitive reason.
2. The application rehydrates an enabled `Ready` profile and requires fresh Windows verification.
3. A value-free authorization audit commits before the provider request.
4. The provider authenticates for this operation and posts to the exact account retrieval endpoint.
5. The value is revealed for ten seconds or copied through owner-aware timed clipboard clearing.
6. A value-free result audit commits. If it cannot commit, the returned value is disposed and not
   presented.
7. The initial CyberArk provider never writes the value to the offline-value cache.

## Trust assumptions

- The operating system and signed application package are trusted.
- The local user account may be compromised; platform unlock reduces but does not eliminate this risk.
- Azure remains the source of truth.
- CyberArk remains the source of truth for its accounts, effective permissions, and server audit.
- Connected identities are authorized only for their existing Azure permissions.
- Plugins are untrusted until a formal trust model exists.
- Clipboard consumers and screen-capture utilities are outside the app's full control.
