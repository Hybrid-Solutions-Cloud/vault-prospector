# Vault Prospector

Vault Prospector is a local-first, cross-platform application for securely discovering, indexing, searching, and optionally caching secrets, keys, and certificates from Azure Key Vault across multiple Microsoft Entra tenants, subscriptions, identities, and client environments.

The product is intended to replace repetitive Azure portal navigation and ad hoc Azure CLI commands with a fast, searchable, offline-capable desktop and mobile experience.

> **Project status:** Architecture and research phase. No production release is available yet.

## Core vision

Vault Prospector will provide:

- One secure application experience for Windows, macOS, and iOS.
- Multiple connected Azure identities.
- Discovery across multiple Microsoft Entra tenants and Azure subscriptions.
- Fast local search across Key Vault names, object names, tags, versions, expiration dates, and source context.
- A local encrypted metadata index.
- Optional, explicitly enabled offline caching of selected secret values.
- OS-backed application unlock using Windows Hello, Apple Keychain, Secure Enclave, or equivalent platform capabilities.
- Clear identity, tenant, subscription, vault, and workspace boundaries.
- Secure clipboard behavior and automatic clipboard clearing.
- Extensible support for additional secret providers in the future.

## Guiding principles

1. **Secure by default** — secret values are never cached unless the user explicitly enables caching.
2. **Local first** — the app does not require a hosted backend for its primary workflow.
3. **Least privilege** — Azure access is limited to permissions already granted to each connected identity.
4. **Identity clarity** — every result clearly identifies the account, tenant, subscription, vault, and version from which it came.
5. **Offline with intent** — metadata search is available offline; secret-value access offline is opt-in and policy controlled.
6. **Cloud optional** — synchronization services may be added later, but are not required for the initial product.
7. **Provider extensibility** — Azure Key Vault is the first provider, not a permanent architectural limitation.

## Proposed technology direction

The initial recommendation is:

- .NET 9
- Avalonia UI
- C#
- MVVM
- Microsoft Authentication Library
- Azure SDK for .NET
- SQLite with an encrypted storage strategy
- Platform-specific secure key storage
- Clean Architecture-inspired project boundaries

These decisions remain subject to validation through the research spikes in `docs/spikes`.

## Repository structure

```text
vault-prospector/
├── README.md
├── CONTRIBUTING.md
├── SECURITY.md
├── LICENSE
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── product/
│   ├── security/
│   └── spikes/
└── src/
    └── README.md
```

## Initial milestones

### Milestone 0 — Foundation

- Confirm architecture and technology decisions.
- Complete authentication, storage, enumeration, and platform feasibility spikes.
- Establish the threat model and security requirements.
- Scaffold the solution and CI pipeline.

### Milestone 1 — Connected metadata search

- Sign in with Microsoft Entra ID.
- Add and manage multiple Azure identities.
- Discover accessible tenants, subscriptions, and Key Vaults.
- Index vault metadata and object metadata.
- Search by name, tag, tenant, subscription, workspace, type, and expiration status.

### Milestone 2 — Secure value retrieval

- Retrieve secret values on demand.
- Implement secure clipboard workflows.
- Record local, privacy-preserving access history.
- Add favorites and recently used items.

### Milestone 3 — Offline value cache

- Allow users to opt selected values into encrypted offline storage.
- Require local biometric or device credential unlock.
- Support configurable expiration and automatic removal.
- Make offline status and staleness visible.

### Milestone 4 — Mobile and ecosystem integration

- Deliver iOS workflows.
- Research Password AutoFill and credential-provider integration.
- Add Windows integration where platform APIs permit it.
- Add extension points for future providers.

## Documentation

- [Project charter](docs/product/project-charter.md)
- [Product requirements](docs/product/product-requirements.md)
- [Roadmap](docs/product/roadmap.md)
- [Backlog](docs/product/backlog.md)
- [Architecture overview](docs/architecture/architecture-overview.md)
- [Domain model](docs/architecture/domain-model.md)
- [Threat model](docs/security/threat-model.md)
- [Security requirements](docs/security/security-requirements.md)
- [Architecture decision records](docs/adr/README.md)
- [Research spikes](docs/spikes/README.md)
- [Glossary](docs/glossary.md)

## Important security warning

Vault Prospector will handle highly sensitive information. Until a release has completed security review, external penetration testing, and a formal release process, it must not be treated as an approved enterprise secret-management product.
