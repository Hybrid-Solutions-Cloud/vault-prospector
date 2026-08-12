# Current task

Fix and publish AB#7337 before resuming any other issue or feature work.

Current implementation branch: `fix/entra-rdp-unlock` from public Preview 10 source
`a0168c390568af1fa679ce6c02f6c43f46c66242`.

- Exact installed Preview 10 fails at **Verify and continue** for the current Entra-backed Windows
  account in RDP, before application Entra sign-in is reachable.
- Windows Hello is unavailable in the remote session; remote credential policy is allowed; the
  fallback returns `RemoteCredentialFailed`.
- Preview 11 proved that supplying the missing `AzureAD` authority was insufficient. Windows
  Security and AAD Operational events showed `0xC0000250`, `interaction_required`, and
  `AADSTS50076`: Conditional Access required MFA, which password-only `LogonUserW` cannot perform.
- Entra-backed remote sessions now use a fresh system-browser Entra sign-in that can satisfy MFA.
  The authenticated Entra object ID must equal the object ID encoded in the current Windows cloud
  SID. A different account cannot unlock the current profile, and the unlock token is not persisted.
- Local and Active Directory domain sessions retain the native credential verifier. They are not a
  prerequisite or workaround for Entra-only VDI estates.
- The locked screen now displays the exact installed informational version.
- The revised Release gate passes locked restore, vulnerability inspection, formatting, a
  zero-warning build, and all 482 tests. Browser-extension tests and production build also pass.

Next:

1. Commit the exact reviewed source and package `0.3.0-preview.12`.
2. Install that exact MSI on the current Entra-joined RDP VM and prove successful current-account
   MFA unlock plus a redacted authorized diagnostic event.
3. Push, run protected-branch CI, merge only after the exact head passes, then tag and publish the
   immutable Preview 12 artifacts.

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
