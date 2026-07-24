# CyberArk Phase 12 evidence — 2026-07-24

## Result

The source tree contains an internal CyberArk Privilege Cloud Shared Services provider with a
separate desktop workflow. It is not yet a supported production integration. Live tenant,
independent-review, and exact signed-artifact gates remain open.

## Implemented boundary

- ADR-0015 selects Privilege Cloud and Identity service-user authentication; Conjur and
  on-premises/custom endpoints are explicitly out of scope.
- CyberArk profiles, safes, accounts, versions, direct permission evidence, failures, and audit are
  explicit provider models rather than Azure aliases.
- SQLCipher schema v6 stores configuration and metadata only.
- The service-user client credential is in a per-profile DPAPI `CurrentUser` file with
  profile-specific entropy; replacement validates first.
- Identity and Privilege Cloud URLs are HTTPS/root/default-port and restricted to the supported
  CyberArk production domains. Redirects and off-origin/API-path pagination are rejected.
- Metadata and value responses have byte, page, item, and version bounds. Server bodies do not
  enter exception messages.
- Metadata sync does not call value retrieval.
- Explicit account/version retrieval requires a ready profile, non-sensitive reason, fresh Windows
  verification, pre-request value-free audit, and `show`/`copy`.
- A value is disposed if post-provider audit cannot commit. Lock/background/system boundaries hide
  UI presentation.
- Local revocation persists a disabled/revoked state before deleting the credential so deletion
  failure remains fail-closed. Removing a profile removes its credential and synchronized metadata
  while retaining value-free local audit.

## Automated evidence

- `pwsh ./scripts/Build.ps1 -Configuration Release` passed on 2026-07-24: locked restore,
  vulnerable-package scan, format verification, Release build with zero warnings/errors, coverage
  collection, and 342/342 .NET tests.
- CyberArk provider contract/redaction suite: 12 tests passing, including service-user exchange,
  mapping, bounded responses, malformed JSON, endpoint/redirect/pagination rejection, and
  401/403/429/5xx safe failure mapping.
- Application suite: 66 tests passing after addition of validation-order, replacement rollback,
  verification denial, value-free audit, audit-failure disposal, and fail-closed local revocation
  coverage.
- Platform suite: 50 tests passing, including DPAPI round trip/removal and cross-profile replay
  rejection.
- Infrastructure suite: 54 tests passing, including schema v6, successful-sync revalidation,
  CyberArk round trip/audit retention, encrypted-file plaintext canary, and cross-profile atomic
  rollback checks.
- App suite: 85 tests passing, including a reachable CyberArk tab with named profile, account,
  reason, protected preview, and audit controls.
- Browser extension: 6/6 tests passing and the production bundle builds.
- An unsigned local `0.1.1-preview.12` MSI packaged successfully. Shortcut/icon,
  rollback-safe-upgrade, and browser-host/policy inspections passed; the MSI File table includes
  `VaultProspector.Providers.CyberArk.dll`. This disposable local package is build evidence only,
  not a release candidate.

PR [#11](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/11) merged verified head
`31a4f3918cc92d529150bd8578047989c562497c` as merge commit
`6b9d5cd85ca453e561c34d966bfc47efc581b551`. Exact-commit CI run
[`30069509556`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30069509556)
passed both required jobs. It repeated full-history secret scanning, locked restore, format, build,
342 tests, extension build/tests, PowerShell parsing, MSI/package validation, and vulnerable
dependency inspection. The PR received a deliberate maintainer review before merge. CI did not
publish a signed or release candidate, and this result does not close any gate below.

## Open release gates

- Governed non-production Privilege Cloud tenant and least-privilege service user.
- Live validate, safe/account/version/permission sync, current and historic retrieval, and
  server-audit correlation.
- Confirmation, ticketing, dual-control, group/role, OLAC, disabled account, and denied-safe matrix.
- 401/403/404/429/5xx, token expiry, credential rotation/revocation, pagination, and cancellation
  against the live service.
- Packet, log, crash-dump, DPAPI/reinstall/recovery, clipboard, and Windows-boundary review.
- Representative-user and Narrator/NVDA/keyboard/High Contrast validation.
- Independent security review with no open critical/high findings.
- Exact signed MSI/package, clean-machine, SBOM, provenance, and CI evidence.
