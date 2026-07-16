# ADR: .NET and Avalonia technology direction

- **Status:** Proposed
- **Date:** 2026-07-16

## Context

The application must support Windows first, with macOS and iOS as important targets. The team needs strong Azure SDK and Microsoft identity support, shared business logic, native platform integration, and a maintainable UI model.

## Decision

Use C# and .NET as the primary language and runtime. Use Avalonia UI as the leading desktop UI candidate, subject to spikes that validate:

- Windows quality.
- macOS quality.
- iOS maturity and deployment.
- Accessibility.
- Native secure-storage integration.
- MSAL compatibility.
- Packaging and signing.
- Application size and startup performance.

If iOS limitations are material, retain the .NET domain and application layers while allowing a separate native SwiftUI shell.

## Consequences

The project gains strong Azure integration and broad code sharing. Avalonia platform maturity must be validated before final acceptance. A hybrid UI strategy remains possible.

## Alternatives considered

- Defer the decision until implementation.
- Use a simpler model that covers only one identity and tenant.
- Introduce a hosted backend immediately.
- Couple the design directly to current Azure SDK types.

Alternatives remain available only through a superseding ADR.
