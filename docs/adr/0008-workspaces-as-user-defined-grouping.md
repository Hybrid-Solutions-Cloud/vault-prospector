# ADR: Workspaces as user-defined grouping

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

Users need a way to organize resources by customer, employer, project, lab, or personal use. Azure tenants and subscriptions do not always match the user's mental or operational grouping.

## Decision

Introduce local workspaces.

A workspace can reference identities, tenants, subscriptions, vaults, and saved searches. It does not duplicate indexed resources and does not alter Azure resources.

Examples:

- Personal
- TierPoint
- Customer A
- Demo
- Community Projects

## Consequences

The application supports human-friendly organization without changing Azure. Workspace policy can later control offline caching and display. Additional local relationship management is required.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
