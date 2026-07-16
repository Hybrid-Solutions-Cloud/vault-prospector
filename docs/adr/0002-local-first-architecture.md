# ADR: Local-first architecture

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

The primary use case is fast access from a trusted personal or work device, including metadata search when disconnected. A hosted backend would create additional custody, privacy, compliance, availability, and operating-cost concerns.

## Decision

The initial release will use a local-first architecture.

- Metadata is stored in a locally encrypted index.
- Authentication tokens use supported local token-cache mechanisms.
- Secret values are fetched directly from Azure.
- Selected values may be cached locally only after explicit user action.
- No project-controlled service is required for normal operation.

## Consequences

The product can work without a service subscription and avoids central custody of customer secrets. Cross-device synchronization, centralized policy, and shared workspaces become more difficult and are deferred.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
