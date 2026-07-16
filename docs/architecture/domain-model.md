# Domain Model

## Entity relationships

```text
LocalProfile
 ├── ConnectedIdentity*
 │    ├── TenantAccess*
 │    │    └── SubscriptionAccess*
 │    │         └── VaultAccess*
 │    └── AuthenticationState
 ├── Workspace*
 │    └── WorkspaceResourceLink*
 ├── Vault*
 │    └── VaultObject*
 │         └── VaultObjectVersion*
 ├── SyncRun*
 └── OfflineCacheEntry*
```

## LocalProfile

Represents the local application profile on a device.

Suggested properties:

- ProfileId
- DisplayName
- CreatedAt
- Settings
- PolicyState
- DatabaseSchemaVersion

## ConnectedIdentity

Represents one configured Azure authentication identity.

Suggested properties:

- ConnectedIdentityId
- ProviderType
- AccountIdentifier
- UsernameHint
- DisplayName
- HomeTenantId
- AuthenticationState
- LastInteractiveAuthentication
- IsEnabled

The domain must not store raw access or refresh tokens.

## TenantAccess

Represents a tenant context reachable by a connected identity.

Suggested properties:

- TenantAccessId
- ConnectedIdentityId
- TenantId
- DisplayName
- TenantType
- LastValidatedAt
- Status

## SubscriptionAccess

Represents a subscription reachable through an identity and tenant context.

Suggested properties:

- SubscriptionAccessId
- TenantAccessId
- SubscriptionId
- DisplayName
- State
- IsSelected
- LastDiscoveredAt

## Vault

Represents a provider vault independent of which identities can access it.

Suggested properties:

- VaultId
- ProviderType
- ProviderResourceId
- Name
- TenantId
- SubscriptionId
- ResourceGroup
- Location
- Tags
- Properties
- LastIndexedAt

## VaultAccess

Maps a connected identity and tenant context to a vault.

Suggested properties:

- VaultAccessId
- VaultId
- ConnectedIdentityId
- TenantId
- AccessStatus
- LastValidatedAt
- LastFailureCategory
- PreferredRank

Multiple access paths may exist for one vault.

## VaultObject

Represents a secret, key, certificate, or future provider object.

Suggested properties:

- VaultObjectId
- VaultId
- ProviderObjectName
- ObjectType
- Enabled
- Tags
- ContentType
- CreatedAt
- UpdatedAt
- ExpiresAt
- CurrentVersionId
- LastIndexedAt
- IsDeletedOrUnavailable

## VaultObjectVersion

Represents a provider-specific object version.

Suggested properties:

- VersionId
- VaultObjectId
- ProviderVersion
- Enabled
- CreatedAt
- UpdatedAt
- ExpiresAt
- IsCurrent
- MetadataFingerprint

No value is present in the metadata model.

## Workspace

A user-defined grouping layer.

Suggested properties:

- WorkspaceId
- Name
- Description
- Icon
- SortOrder
- CachePolicyOverride

## WorkspaceResourceLink

Links a workspace to one of:

- Connected identity.
- Tenant.
- Subscription.
- Vault.
- Saved search.

Links avoid duplicating indexed objects.

## OfflineCacheEntry

Represents an explicitly cached version.

Suggested properties:

- OfflineCacheEntryId
- VaultObjectVersionId
- EncryptedPayloadReference
- CachedAt
- ExpiresAt
- LastUnlockedAt
- CachePolicyId
- SourceMetadataFingerprint

The value itself should be stored by the protected value store rather than directly in general-purpose ORM entities.

## SyncRun

Suggested properties:

- SyncRunId
- Scope
- StartedAt
- CompletedAt
- Status
- Counts
- NonSensitiveErrors
- CancellationReason
