# Architecture Overview

## Architectural style

Vault Prospector is planned as a local-first, modular application with explicit boundaries between:

- User interface.
- Application orchestration.
- Core domain.
- Provider integrations.
- Local persistence.
- Authentication.
- Platform security capabilities.
- Optional plugins.

The initial implementation will use a Clean Architecture-inspired dependency direction. This does not require every enterprise pattern; the goal is to protect the domain and prevent UI, Azure SDK, database, or operating-system concerns from becoming inseparable.

## Major components

### Application shell

Responsibilities:

- Navigation.
- Search experience.
- Identity and workspace management.
- Sync status.
- Settings.
- Local unlock requests.
- Accessible desktop and mobile interaction.

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
- Apple Keychain and LocalAuthentication integration.
- Secure random generation.
- Data protection.
- Clipboard control.
- Screen-capture and background-state mitigations where available.

### Plugin host

Responsibilities:

- Load trusted provider extensions.
- Expose narrow provider contracts.
- Enforce version compatibility.
- Prevent plugins from receiving unrelated provider data.

Dynamic third-party plugins may be postponed until a safe trust and signing model is defined.

## Data flow: metadata synchronization

1. The user selects an identity, workspace, or global sync.
2. The application obtains a token through the authentication adapter.
3. The Azure provider discovers selected subscriptions and vaults.
4. The provider enumerates object metadata without requesting values.
5. Records are normalized into domain index models.
6. The index transaction applies additions, updates, tombstones, and checkpoints.
7. Search becomes immediately available against the local index.
8. Sync errors are retained as non-sensitive diagnostics.

## Data flow: secret retrieval

1. The user selects a result.
2. The application resolves a valid identity-to-vault access path.
3. Policy determines whether local unlock is required.
4. The Azure provider requests the selected version.
5. The value exists in memory only.
6. The user may reveal or copy it.
7. The UI clears displayed content and application buffers as soon as practical.
8. The value is not persisted unless the user explicitly enables offline caching.

## Trust assumptions

- The operating system and signed application package are trusted.
- The local user account may be compromised; platform unlock reduces but does not eliminate this risk.
- Azure remains the source of truth.
- Connected identities are authorized only for their existing Azure permissions.
- Plugins are untrusted until a formal trust model exists.
- Clipboard consumers and screen-capture utilities are outside the app's full control.
