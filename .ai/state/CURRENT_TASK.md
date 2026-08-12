# Current task

Implement and validate the 2026-08-11 support-bundle remediation batch without claiming that a
local build proves the installed Azure workflows.

Current implementation branch: `chore/tracking-reconciliation-20260811`.

- GitHub #91 / ADO AB#7299: background metadata sync now processes every enabled, ready,
  policy-allowed connected identity instead of only the selected identity.
- GitHub #92 / ADO AB#7301 and Story AB#7305: workload Administration uses the real Azure
  subscription ID and an aggregated subscription/name/tenant/account picker populated from all
  ready interactive connections.
- GitHub #93 / ADO AB#7303: the actionable error banner has a real dismiss command.
- Existing GitHub #40 / ADO AB#5573, Tasks AB#7307–#7308: support bundles retain pseudonymous
  scope, bounded correlation ID, and allowlisted error category so the remaining missing-object
  failures can be classified safely.
- Discovered workload identities remain Administration candidates. They do not appear in Find
  Secrets until explicitly configured as a supported connection and synchronized; Find Secrets
  contains vault objects, not identities.
- Exact .NET SDK 10.0.302 was installed under `D:/tmp/dotnet-vp-10`. The Release gate passes a
  locked restore, vulnerability scan, formatting, zero-warning build, and all 459 tests.
- A prior MSAL 4.87 dependency bump left three downstream NuGet lock files stale; those locks were
  mechanically reconciled so the locked gate could execute.

Next:

1. Commit and push this implementation and tracking update.
2. Run protected-branch CI and review the exact head.
3. Package/install the corrected build and repeat sync plus managed-identity discovery with the
   product owner's three connected identities.
4. Generate a new support bundle after that run and use AB#7308 to separate any remaining Azure
   access failures from application defects.

---

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
