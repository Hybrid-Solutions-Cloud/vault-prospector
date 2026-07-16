# ADR: Separate application unlock from Azure identities

- **Status:** Accepted
- **Date:** 2026-07-16

## Context

A user may want one consistent way to unlock the application while connecting many Azure identities that span personal, employer, customer, and guest tenants. Using one Azure identity as the universal application identity would incorrectly couple local access to one provider account.

## Decision

Separate two concerns:

1. **Local application unlock** protects local encrypted data and uses platform authentication.
2. **Connected Azure identities** authorize discovery and retrieval from Azure.

A future optional application account may use Entra ID, Apple, Google, or another provider, but it will not replace Azure authorization contexts or become required for the initial local-first product.

## Consequences

Users can maintain one local experience while connecting multiple Azure identities. Product terminology and UI must clearly distinguish unlocking the app from authenticating to Azure.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
