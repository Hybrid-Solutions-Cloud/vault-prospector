# Project Charter

## Product name

Vault Prospector

## Problem statement

Azure professionals frequently work across many Microsoft Entra tenants, Azure subscriptions, projects, and customer environments. Azure Key Vault securely stores secrets, keys, and certificates, but the day-to-day retrieval experience becomes inefficient at scale.

Common pain points include:

- Repeated Azure portal navigation.
- Weak cross-vault and cross-subscription search.
- Repeated Azure CLI sign-in and context switching.
- Difficulty determining which identity can access which vault.
- No unified local index across multiple tenants.
- Limited offline access.
- Poor visibility into expiration, version, ownership, and project context.
- Risky copying of values into temporary notes or chat windows.

## Mission

Create a secure, local-first application that gives authorized users fast and understandable access to their Azure Key Vault estate across multiple identities and tenants while preserving least privilege and making offline access explicit, controlled, and auditable.

## Target users

- Cloud architects.
- Azure administrators.
- Consultants and managed service providers.
- Developers working across multiple Azure environments.
- Security and platform engineering teams.
- Individuals with personal, customer, demo, and employer tenants.

## Goals

- Support multiple Microsoft Entra identities.
- Discover Azure tenants, subscriptions, and Key Vaults available to each identity.
- Build a fast local metadata index.
- Provide instant cross-vault search.
- Display complete source context for every result.
- Retrieve values securely and only when requested.
- Support optional encrypted offline caching.
- Use platform-native secure storage and local unlock capabilities.
- Provide a provider model that can support additional secret systems later.
- Operate without a project-hosted backend in the initial release.

## Non-goals for the initial release

- Replacing Azure Key Vault as the authoritative system of record.
- Editing or rotating secrets.
- Creating Azure RBAC assignments.
- Bypassing Azure authorization.
- Sharing secrets between users.
- Acting as a general-purpose enterprise password manager.
- Synchronizing secret values through a project-controlled cloud service.
- Supporting non-Azure providers before the Azure workflow is stable.

## Success measures

- A user with ten or more tenants can connect identities and understand all discovered access paths.
- Metadata searches return useful results in under one second on supported devices.
- Secret values are never cached without explicit user action.
- The application can identify stale data and failed identity contexts.
- All sensitive local data is encrypted using platform-appropriate protections.
- Security-sensitive behaviors have automated tests and documented threat mitigations.
