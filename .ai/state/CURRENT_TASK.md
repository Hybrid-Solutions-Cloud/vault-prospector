# Current task

Validate public Preview 18 feedback before resuming lower-priority work.

Current public source: merge commit `f98174b9211b4889e635558cc7237d102c7f0730`, immutable tag
`v0.3.0-preview.18`.

- Preview 18 removes in-app package download, retention, elevation, and launch. Settings performs
  bounded discovery only and links to the public release history and verification guide.
- Identity-workspace search now checks every enabled, non-removed access row for the linked
  identity instead of applying the link after choosing the preferred display/retrieval identity.
- Clipboard Copy without fresh verification requires a shared application-session authorization
  in the service layer and rechecks it after Azure retrieval before clipboard output.
- Object-details copy now distinguishes the unlocked-session Copy boundary from Reveal's
  configured verification policy.
- Release validation passes 496/496 tests, zero warnings/errors, and dependency audit; browser
  validation passes 6/6 plus production build. Local Preview 18 candidate MSI SHA-256 is
  `A854FF37B99B0A233D0916E39F564ACD6D8426BBC41E449201E6941C080CB2CA`; all three MSI validators
  passed. It upgraded installed Preview 17 with exit code 0, Windows reports `0.3.18`, all five
  non-log state hashes are unchanged, and the Program Files executable launches.
- PR #105, exact-main CI run 31753731408, and immutable-tag release run 31754264358 passed. The
  public release contains 16 immutable assets. Fresh downloads of all five packages matched their
  adjacent checksums and passed keyless Sigstore verification. Public MSI SHA-256 is
  `B3D87A95ED664ACD5323A74730FEE2A763B6ACDBED2E40F10346A81557E164C7`.

- Three interactive identities synchronized with isolated errors on the exact installed Preview 12
  candidate. Safe logs show authentication-heavy failures; one identity found 15 vaults and
  emitted exactly 45 authentication-category scope failures.
- Preview 13 passed the full local build/package/installer gate and proved each Key Vault request
  receives the matching tenant context, but live validation did not improve the first identity:
  it still produced 7 items and 7 isolated errors (2 Azure-request, 5 authentication). Preview 13
  remains unpublished.
- The remaining failure occurs when correctly tenant-scoped silent MSAL acquisition reports that
  the guest/resource tenant requires interaction. Preview 12/13 convert each such result into a
  partial metadata error and never let a user-triggered sync satisfy that tenant policy.
- Preview 14 attempted automatic interactive recovery from foreground sync. One sync opened at
  least four system-browser prompts for the same visible account because it crossed multiple
  tenant/resource authorization contexts. The product owner rejected that behavior; Preview 14
  was stopped, never published, and the VM was restored to exact public Preview 12 with all six
  local data files preserved byte-for-byte.
- Automatic interactive authentication is removed from normal sync, background sync, and
  failed-scope retry in `2cff4ec`. All three paths are constrained to silent token acquisition and
  may report partial authorization errors without opening a browser or consent window.
- Exact local Preview 15 from `2cff4ec` passed all package/installer gates and is installed on the
  VM. Its three latest syncs completed without an observed prompt cascade and reported 200 objects
  / 23 raw operation errors, 178 / 5, and 88 / 75. Those counts represent individual failed
  metadata operations, not 23, 5, or 75 distinct Azure targets.
- Commit `c4bb415` groups failures by tenant, subscription, or vault; resolves pseudonymous log
  scopes to locally encrypted display names; labels the failed operations; and retries the complete
  selected target. It also implements About-page buttons for the public user guide, roadmap,
  changelog, release verification guide, and release history. All five URLs returned HTTP 200.
- The governed Release gate passes 490/490 tests, zero warnings/errors, and no vulnerable NuGet
  packages at `c4bb415`. Browser tests pass 6/6 and its production build succeeds.
- Preview 12 publicly resolved the preceding Entra RDP unlock blocker AB#7337 on the specifically
  tested VM/session/account/policy; broader VDI coverage remains open in release readiness.
- The next source candidate removes redundant clipboard authentication by using the existing
  unlocked application session for Copy by default. An explicit Settings override can require
  fresh verification for each copy; Reveal, enterprise clipboard policy, and auto-clear remain
  separate and enforced.
- Workspaces now have a self-contained member editor with visible identity, tenant, subscription,
  and vault pickers plus removal. Administration workload discovery uses the selected
  subscription's tenant and account for both ARM managed identities and Graph service principals,
  with privacy-safe operation-specific diagnostics.
- The governed Release gate passes 495/495 tests with zero warnings/errors and no vulnerable NuGet
  packages. Browser tests pass 6/6 and the production browser build succeeds.
- Exact Preview 17 from source commit `f4c76ccf41691cff8579c8d732b753b177a2aa6d` passed all
  three MSI validators and is installed. MSI SHA-256 is
  `950DDD29966319930375073D702D3CD65BA803773AD8021EB3A929EAF6F22C59`; Windows reports
  DisplayVersion `0.3.17`, and all five non-log state files remained byte-for-byte unchanged.
- PR #103, exact-main CI run 31669818796, and immutable-tag release run 31670200914 passed. The
  public release contains 16 immutable assets, and fresh public downloads matched all five adjacent
  checksums. Public MSI SHA-256 is
  `AE40CCEC74F680A733ECFD909BD1DB9AFA2D912612AE4532F5D670CA36D71C96`.

Next:

1. In one unlocked session, copy two secrets and confirm neither copy prompts; then enable the
   explicit per-copy verification setting and confirm it restores the prompt.
2. Validate the self-contained Workspace editor and repeat managed-identity/service-principal
   discovery using the explicit Administration subscription/tenant/account picker.
3. Sync the three existing identities and confirm zero browser/consent prompts plus named grouped
   target rows in Connections; verify the About-page links from the installed binary.
4. Preserve partial authorization errors honestly while designing any future tenant authorization
   as an explicit, user-initiated workflow with a preview of how many tenant prompts may be needed.
5. Triage new privacy-safe logs and product-owner feedback against exact public Preview 17.

---

Implement, deliver, and validate the 2026-08-12 multi-tenant discovery correction without claiming
that a local build proves the installed Azure workflows.

Current implementation branch: `fix/multi-tenant-subscription-discovery-20260812`.

- GitHub Bug #97 / ADO AB#7310 and Tasks AB#7311–#7312 track the defect.
- Azure discovery now enumerates accessible tenants first and uses an explicit tenant-qualified
  ARM credential for each tenant's subscription request.
- A failed tenant is reported with a pseudonymous scope and does not discard successful tenants.
- Tenant inclusion is persisted in encrypted metadata alongside existing subscription and vault
  inclusion. The Connections UI supports include/exclude for tenants and shows each subscription's
  tenant.
- Schema v8 adds `tenants.is_selected` with a default-included v7 migration and preserves explicit
  choices on rediscovery.
- The exact local Release gate passes locked restore, vulnerability inspection, formatting,
  zero-warning build, and all 465 tests.

Next:

1. Commit/push, open the protected-branch PR, and run CI on the existing documented HCS runners.
2. Merge only after the exact head passes.
3. Publish/install the next Preview through the existing release path and verify the reported
   5–6 tenant account, tenant/subscription selection, and expected Key Vault inventory.

---

Implement and validate the 2026-08-11 support-bundle remediation batch without claiming that a
local build proves the installed Azure workflows.

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
