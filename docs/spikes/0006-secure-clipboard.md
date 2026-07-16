# Research Spike: Secure clipboard behavior

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Document and test clipboard behavior for secrets on Windows, macOS, and iOS.

## Questions

- Can the app reliably clear only the value it placed?
- How does Windows clipboard history behave?
- How does Apple Universal Clipboard behave?
- Can policy disable copying?
- What user warnings are accurate?
- Are there safer alternatives such as AutoFill or direct injection?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

Platform-specific recommendations and UX text that do not overstate protection.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
