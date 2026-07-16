# ADR: Product scope and guiding principles

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

Vault Prospector must solve a focused problem: secure discovery, search, and retrieval across Azure Key Vault resources that the user is already authorized to access.

The project could easily expand into a password manager, secret-sharing platform, Azure administration suite, or hosted synchronization service. That expansion would increase security scope and delay the core workflow.

## Decision

The initial product will:

- Be secure by default.
- Be local first.
- Treat Azure as the source of truth.
- Support multiple Azure identities and tenants.
- Index metadata without automatically retrieving values.
- Make offline secret caching explicit and optional.
- Avoid a required project-hosted backend.
- Design for future providers without implementing them prematurely.

The initial product will not create or rotate secrets, assign Azure permissions, or share values between users.

## Consequences

The project receives a clear security and product boundary. Some attractive features are postponed. Future scope changes require new ADRs and threat-model updates.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
