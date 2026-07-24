# G-08 operational-readiness baseline — 2026-07-24

## Scope and truth boundary

This record covers the source-level operational-readiness baseline introduced by implementation
commit `3410f77f71a374eca684b6d97f3936a8693ee1d3`. It does not mark G-08 Passed and does not claim that
scheduled GitHub-hosted monitoring, backup staffing, the full runbook exercise, or Authenticode key
operations have occurred.

## Implemented controls

- `ops/operational-readiness.json` is the canonical machine-readable contract for owners, response
  targets, cadences, supported release, dependency coverage, runtime lifecycle, credential/signing
  controls, public monitors, and open evidence.
- `scripts/Test-OperationalReadiness.ps1` validates that contract against repository documents and
  automation, emits a JSON report, warns 120 days before runtime end of support, and fails after
  end of support or on a required-contract/public-endpoint failure.
- `.github/dependabot.yml` proposes weekly desktop/mobile NuGet, browser/design npm, and pinned
  GitHub Actions updates without auto-merge.
- `.github/workflows/operational-readiness.yml` runs weekly or manually, inspects direct/transitive
  desktop NuGet vulnerabilities, performs live endpoint checks, and retains its report for 90 days.
- Normal CI runs the same source contract without network checks.
- `docs/support-lifecycle.md` defines Preview support, supersedence, withdrawal, future GA
  end-of-support notice, emergency withdrawal, channels, and dependency/platform maintenance.
- The operations runbook now defines monitor disposition and 30/90-day inventory/exercise cadences.

## Local evidence

The implementation tree completed:

```powershell
pwsh ./scripts/Test-OperationalReadiness.ps1 `
  -CheckPublicEndpoints `
  -OutputPath artifacts/operational-readiness/3410f77f71a374eca684b6d97f3936a8693ee1d3.json
```

Observed at `2026-07-24T09:56:20.2637443Z`:

- source commit: `3410f77f71a374eca684b6d97f3936a8693ee1d3`;
- contract checks: 35 passed, 0 failed;
- public endpoints: current Preview release page HTTP 200, current MSI checksum HTTP 200, and
  Preview feedback page HTTP 200;
- findings: 0 errors and one actionable `RUNTIME_SUPPORT_WINDOW` warning; and
- current supported evaluation version: `0.1.1-preview.1`, with production support explicitly
  false.

A deterministic negative check using `-AsOfUtc 2026-11-11T00:00:00Z` exited nonzero and emitted
`RUNTIME_END_OF_SUPPORT`, proving the recorded desktop runtime deadline fails closed.

The same source tree completed the locked Release gate:

- locked restore;
- structured direct/transitive NuGet vulnerability inspection;
- formatting verification;
- build with 0 warnings and 0 errors; and
- 343/343 tests:
  Domain 4, Application 66, Infrastructure 54, CyberArk 12, Security 1, Platform 50, Azure 27,
  BrowserProtocol 36, App 85, and BrowserHost 8.

An initial command-wrapper timeout left its child build/test process active; two overlapping retries
failed on DLL file locks held by that identified process tree. After stopping only those
worktree-scoped stale processes, the unchanged source passed. Those two attempts are local
orchestration contamination, not product test failures.

## Runtime lifecycle finding

The official .NET support policy listed .NET 9 in maintenance with end of support on
`2026-11-10` and .NET 10 LTS active through `2028-11-14` when this record was created:
<https://dotnet.microsoft.com/platform/support/policy/dotnet-core>.

The desktop currently targets .NET 9, so migration to a supported runtime is required before any
Vault Prospector support period extending beyond `2026-11-10`. The mobile source is already pinned
to .NET 10.

## Remaining G-08 evidence

- Assign and record a backup support/security operator.
- Obtain successful retained hosted runs of the weekly monitor. GitHub-hosted jobs are currently
  rejected before their first step because the organization reports a payment/spending-limit
  problem; that is not a source validation result.
- Exercise detection, severity classification, private communication, withdrawal, package-manager
  action, credential decision, recovery, and closure against the exact GA candidate.
- Approve and test Authenticode key custody, expiration, rotation, timestamping, revocation, and
  compromise response.
- Repeat the full operational review on the exact signed public candidate.

## Initial hosted PR state

PR [#18](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/18) opened from initial
head `9aef1f31ad6b44a66bcdd4e7d18813d7c30f48e9`. CI run
[`30084503284`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30084503284)
created `build-test` and `secret-scan`, but both jobs had zero steps and were rejected before
execution. Their annotations state that recent account payments failed or the spending limit must
be increased. This is organization-hosting evidence only: it neither passes nor fails source
validation. PR #18 must remain unmerged until required exact-head checks execute and pass.
