# Current task

Reconcile Azure DevOps, the canonical PMO inventory, and deployed source without converting
implementation evidence into acceptance or release-readiness claims.

Confirmed on 2026-08-11:

- The private Agile ADO project is named `HCS -Vault Prospector`; its default team is
  `HCS -Vault Prospector Team`.
- ADO contains 248 items: 89 Active, 92 New, 13 Resolved, 53 Closed, and 1 Removed.
- The 89 Active items are assigned to dated iterations: 21 in `2026-Q3-S4`, 29 in
  `2026-Q3-S5`, and 39 in `2026-Q3-S6`; none remains at the root iteration.
- All 124 nonterminal Epic/Feature/User Story/Bug records have Acceptance Criteria.
- AB#6165 and AB#5334 have corrected distinct Acceptance Criteria. AB#5296, AB#5298, and AB#5314
  now state that their implementation is in Preview 8 while retaining the open live-validation
  requirements.
- `v0.3.0-preview.8` is the latest public tag and points to
  `2582a44e50155c80205370c6ec90b9d19eb7a006`.
- No dedicated Preview 8 release-evidence record exists in this repository. Preview 6 remains the
  latest release with complete retained artifact/provenance evidence; do not infer Preview 8
  hashes, asset counts, independent downloads, or installed validation.
- Private-endpoint connectivity AB#6192–6225 is future backlog scope. No corresponding code, ADR,
  threat model, lab evidence, or deployment exists.

Next:

1. Obtain and retain exact Preview 8 artifact/provenance/independent-download evidence, or publish
   an explicitly evidenced replacement Preview.
2. Record exact-package live workflow evidence and close only items whose complete Acceptance
   Criteria pass.
3. Keep AB#6052 (`Roadmap: Create and manage Azure Key Vaults and keys`) in the P4 root backlog.
   Do not decompose or schedule it until the product owner explicitly advances it into a delivery
   target.
4. Decide the supported private-connectivity architecture before starting AB#6211 or later.
5. Record repository attribution for cross-repository work after the other two product repositories
   and the authoritative mapping mechanism are confirmed.
