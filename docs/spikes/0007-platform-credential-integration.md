# Research Spike: Apple and Windows credential integration

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Research whether Vault Prospector can integrate with Apple Password AutoFill, iCloud Keychain-related extension points, Windows credential APIs, browser AutoFill, or Windows Hello without misrepresenting platform capabilities.

## Questions

- Can an iOS credential-provider extension return arbitrary secrets or only website/app credentials?
- What entitlement, extension, and review requirements apply?
- Can Windows Credential Manager be used safely without duplicating secrets?
- Can Windows Hello gate access without becoming a credential store?
- Should integration be read-only, write-only, or avoided?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A platform capability matrix with supported, unsupported, and unsafe approaches.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
