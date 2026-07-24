# Workload authorization evidence — 2026-07-23

## Scope

This record covers the local Phase 5 read-only authorization assessment for a selected discovered
workload identity and one exact Azure Key Vault resource. It does not claim live-tenant validation,
independent security approval, mutation readiness, or released-artifact behavior.

## Implemented path

- The user selects an enabled, ready interactive administrator already connected through the
  app-owned MSAL cache.
- Managed identities are discovered only in an exact subscription. Service-principal listing
  remains behind separate delegated `Application.Read.All` authorization.
- The user selects one discovered candidate, enters one exact Key Vault resource ID, and invokes
  **Assess selected identity permissions**.
- Azure's caller-permissions endpoint reports the selected administrator's effective
  managed-identity attach, managed-identity write, and role-assignment write actions at exact
  resources.
- Applicable candidate role assignments are requested with `assignedTo`, then constrained to
  scopes at or above the vault. Each referenced role definition is read and evaluated using
  `Actions`, `NotActions`, `DataActions`, and `NotDataActions`.
- Applicable deny assignments are evaluated before grants, including exact/ancestor scope,
  `doNotApplyToChildScopes`, direct principals, all principals, exclusions, permission exclusions,
  and conditions.
- Secret/key/certificate metadata evidence is separate from secret-value evidence.
- Every displayed evidence item names the subject, exact scope, state, non-sensitive basis, and UTC
  observation time.

The implementation is recorded in
[ADR-0013](../adr/0013-report-effective-azure-authorization-evidence.md).

## Fail-closed boundaries

- All ARM requests use `GET`; the workflow contains no role, identity, or Key Vault mutation and
  performs no data-plane operation.
- Initial requests and pagination are constrained to HTTPS `management.azure.com`; production
  clients disable automatic redirects. Page, item, and role-definition counts are bounded.
- A role-assignment condition or matching deny condition is not interpreted without concrete
  request/resource attributes.
- An unreadable deny-assignment set or potentially applicable group deny makes candidate access
  incomplete rather than allowed.
- Key Vault access-policy mode is detected and not reinterpreted as Azure RBAC.
- A discovered service principal's credential possession, target configuration, Graph management
  role, and ownership remain unproven.
- Static RBAC evidence never claims that Vault Prospector acquired a candidate credential or
  successfully exercised runtime access.

## Automated verification

Focused Azure-provider verification passed 25/25 tests. New positive and adversarial coverage
includes:

- an inherited subscription-level grant with exact caller permissions;
- unconditional deny precedence over an inherited allow;
- conditional grants remaining conditional;
- unreadable deny assignments preventing an allowed result;
- untrusted ARM pagination rejected before a token is sent to the next page;
- Key Vault access-policy mode remaining incomplete;
- bearer audience and tenant binding; and
- GET-only request inspection.

Application coverage proves that assessment requires an interactive administrator, selected
candidate, and exact vault; replaces the selected row with returned evidence; and maps missing ARM
read authorization to actionable, redacted guidance.

The authoritative repository command was:

```powershell
.\scripts\Build.ps1 -Configuration Release
```

It passed on 2026-07-23 with:

- locked restore;
- no known vulnerable direct or transitive NuGet packages;
- formatting verification;
- Release build with zero warnings and zero errors;
- 218/218 tests across all seven test projects; and
- Cobertura coverage artifacts for all seven test projects.

Per-project totals were Domain 4, Application 49, Infrastructure 49, Platform 25, Azure provider
25, App 65, and Security 1.

The HCS governance drift check was invoked with the exact local path after documentation updates,
but the server returned `Path not found`. No HCS drift pass is claimed; repository registration or
server path visibility remains external follow-up.

## Remaining evidence

Before this capability can satisfy release or GA gates, the project still needs:

- live Azure tests across direct, inherited, transitive-group, custom-role, deny, condition,
  cross-subscription, access-policy, disabled-principal, and insufficient-reader cases;
- independent review of action matching, scope normalization, token forwarding, error redaction,
  and the static/runtime distinction;
- installed-candidate keyboard, screen-reader, scaling, and populated-list validation; and
- exact released-artifact repetition.
