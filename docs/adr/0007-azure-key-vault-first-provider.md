# ADR: Azure Key Vault as the first provider

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

The product vision may later include other secret providers, but Azure Key Vault is the immediate user need and has distinct control-plane, data-plane, identity, and versioning behaviors.

## Decision

Implement Azure Key Vault as the first provider behind narrow provider contracts. Do not force provider-specific features into an overly generic lowest-common-denominator model.

Dynamic third-party plugin loading is deferred until signing, trust, isolation, and permission models are defined.

## Consequences

The first release remains focused. Core contracts can evolve from real Azure requirements. Provider expansion remains possible but is not allowed to weaken security boundaries.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
