# Research Spike: Azure resource discovery strategy

- **Status:** Planned
- **Owner:** Unassigned
- **Created:** 2026-07-16

## Objective

Compare Azure Resource Graph, Azure Resource Manager subscription enumeration, and provider-specific resource listing for discovering accessible Key Vaults across many subscriptions.

## Questions

- Which approach minimizes API calls and latency?
- How are partially authorized subscriptions handled?
- Can Resource Graph query all selected subscriptions in batches?
- What metadata is returned without data-plane access?
- How should throttling and pagination be handled?

## Constraints

- Do not use production secrets.
- Do not commit tenant-specific identifiers or tokens.
- Record exact SDK and operating-system versions.
- Include security and user-experience implications.
- Prefer primary vendor documentation and reproducible tests.

## Deliverable

A benchmark and recommendation using a representative multi-subscription test environment.

## Findings

_To be completed._

## Recommendation

_To be completed._

## Decision impact

List ADRs that should be accepted, changed, or superseded.
