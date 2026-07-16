# ADR: Multi-identity and multi-tenant access model

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

One user may have many Azure identities. One identity may reach many resource tenants through guest access or delegated administration. One vault may be reachable through more than one identity. The app must not flatten these relationships.

## Decision

Model identity, tenant access, subscription access, vault, and vault access path separately.

- A connected identity has one home tenant and zero or more resource-tenant contexts.
- Subscriptions are discovered within a tenant context.
- Vaults are provider resources independent of the identity used to reach them.
- A vault may have multiple access paths.
- Retrieval chooses an explicit valid access path.
- The UI shows identity and tenant context for every retrieval.

## Consequences

The model handles MSP and consulting scenarios correctly. Discovery and persistence become more complex, but ambiguity and accidental cross-customer access are reduced.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
