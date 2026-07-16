# ADR: Separate metadata index from protected value store

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

Metadata and secret values have different sensitivity, lifecycle, search, backup, and deletion requirements. Storing both in one general-purpose database would increase exposure and complicate policy.

## Decision

Use two logical security tiers:

- An encrypted metadata index optimized for discovery and search.
- A protected value store containing only explicitly cached values.

The value store uses envelope encryption or an equivalent design, platform-backed key protection, independent expiration, and separate purge operations.

## Consequences

Metadata search remains fast and broadly available while value caching remains constrained. Storage and migration code becomes more complex. Backup behavior must be tested carefully.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
