# Glossary

## Application identity

The identity used to unlock or personalize Vault Prospector. In the initial local-first release, the device's local authentication boundary is more important than a remote application account.

## Azure identity

A Microsoft Entra user, guest, service principal, managed identity, or future workload identity used to access Azure resources.

## Connected identity

An Azure identity that has been added to Vault Prospector and has a usable authentication context.

## Tenant

A Microsoft Entra tenant associated with an Azure identity or Azure resource.

## Home tenant

The Microsoft Entra tenant in which a user identity originates.

## Resource tenant

The Microsoft Entra tenant containing an Azure subscription or resource that the connected identity can access.

## Subscription

An Azure subscription visible to a connected identity.

## Vault

An Azure Key Vault instance or, in future providers, an equivalent container of secret material.

## Vault object

A secret, key, or certificate stored by a provider.

## Secret value

The sensitive payload of a secret version.

## Metadata

Non-value information such as name, object type, tags, enabled state, creation time, expiration time, version identifier, vault, subscription, and tenant.

## Workspace

A user-defined grouping of identities, tenants, subscriptions, and vaults, such as Personal, TierPoint, Customer A, Lab, or Demo.

## Provider

An integration capable of discovering and retrieving secret material. Azure Key Vault is the first provider.

## Index

The local searchable representation of provider metadata.

## Offline cache

An encrypted local store containing explicitly selected secret values for access when Azure is unavailable.

## Unlock identity

The local user verification mechanism used to decrypt protected application material, such as Windows Hello, Touch ID, Face ID, device passcode, or platform keychain authorization.

## Staleness

The amount of time since metadata or cached content was last successfully synchronized with its provider.
