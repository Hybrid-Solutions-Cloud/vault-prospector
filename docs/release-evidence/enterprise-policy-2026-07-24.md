# Machine-managed enterprise policy evidence — 2026-07-24

## Scope and provenance

This record covers local source commit
`5d20399ce37370213fdf280a2b9ff97918fbf1ef` on
`feature/enterprise-policy`. It implements backlog story 9.6 and the source portion of GA gate
G-06. It is not a GA approval and does not replace governed administrator deployment, live-service
testing, independent review, trusted signing, or exact released-artifact validation.

The policy is read from:

`HKLM\SOFTWARE\Policies\Hybrid Solutions Cloud\Vault Prospector`

The application never writes that path. The local validation host had no policy key, so the
read-only live observation proved the valid unmanaged path without changing the registry.

## Implemented boundary

- Versioned machine policy constrains Microsoft Entra tenant GUIDs, Azure Key Vault and CyberArk
  Privilege Cloud providers, Azure identity types, clipboard use, offline-value enablement, and
  maximum offline-value lifetime.
- Invalid or unreadable enabled configuration denies governed access. `Enabled=0` remains an
  unmanaged state even when Group Policy has removed the enabled-only schema value.
- Application services enforce policy before sign-in/credential validation, Azure/CyberArk
  network access, workload administration, value retrieval, clipboard use, and offline-value use.
- Azure discovery receives the tenant allow-list before subscription/vault enumeration and the
  application filters returned metadata again before persistence. Local search and source lists
  omit disallowed provider/tenant metadata.
- Cleanup operations remain reachable: disable, revoke, purge, and remove are not blocked by a
  newly restrictive provider policy.
- Settings exposes only a bounded, non-sensitive policy summary and cannot weaken the machine
  boundary.
- The portable payload and MSI contain `PolicyDefinitions\VaultProspector.admx` and
  `PolicyDefinitions\en-US\VaultProspector.adml`.

## Automated verification

`pwsh ./scripts/Build.ps1 -Configuration Release` on the exact implementation commit passed:

- locked restore;
- direct/transitive NuGet vulnerability scan;
- formatting verification;
- Release build with 0 warnings and 0 errors; and
- 368/368 tests:
  - Domain 4;
  - BrowserProtocol 36;
  - Application 76;
  - Security 1;
  - Platform 61;
  - Azure provider 29;
  - App 87;
  - BrowserHost 8;
  - Infrastructure 54; and
  - CyberArk provider 12.

`gitleaks git --no-banner --redact` scanned 123 commits and reported no leaks.

The source enterprise-policy readiness check passed 42/42 checks. Against the exact published
directory it passed 44/44, including:

- ADMX and ADML Microsoft Group Policy namespaces;
- machine policy registry scope;
- all six ADMX-to-ADML element references;
- all string and presentation references;
- required enforcement/package/documentation markers;
- read-only live HKLM visibility; and
- both packaged policy templates.

## Disposable exact-source package

Local version `0.1.0-ci.930` was created only for validation:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `VaultProspector-0.1.0-ci.930-win-x64.zip` | 101,790,938 | `CD033441AE37B5579DE96C3C0C396C55BD22256AF5517C8F4229EACE7F0B3834` |
| `VaultProspector-0.1.0-ci.930-win-x64.msi` | 60,496,003 | `9177A473B88DFEDAD5EB0E6C0725A717557DC2F1B2149F0EDA1EB174D931671D` |

Windows Installer database inspection found:

| Long file name | Bytes |
| --- | ---: |
| `VaultProspector.admx` | 2,776 |
| `VaultProspector.adml` | 2,699 |

The exact MSI also passed all three existing deterministic guards:

- browser native-host files, disabled default browser policy, and three registry registrations;
- embedded Start-menu icon (`71,158` bytes); and
- rollback-safe major-upgrade ordering
  (`InstallInitialize=1500`, `RemoveExistingProducts=1501`, `InstallFiles=4000`,
  `InstallFinalize=6600`).

The disposable artifacts are unsigned local evidence and are not release candidates.

## Hosted validation

PR [#21](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/21) was opened using the
HCS GitHub App installation identity. On initial exact head
`930dc361de8999b4320900af5e50f1c88a2e2c4d`, CI run `30088899332` and Mobile CI run `30088899350`
finished as failures, but none of their five jobs started a step. Every job annotation states that
recent account payments failed or the organization spending limit must be increased. This is an
external GitHub Actions infrastructure block and is not a passing or failing code-test result.
The PR remains unmerged until exact-head checks execute and pass.

## Remaining G-06 gates

G-06 remains **In progress**. Required evidence still includes:

1. a named administrator deploying the templates through governed Group Policy and/or Intune;
2. allowed and denied Azure tenant/provider/identity-type scenarios against governed live tenants;
3. allowed and denied CyberArk provider scenarios against a governed non-production tenant;
4. live policy refresh, rollback, malformed/unreadable policy, standard-user, and device-management
   precedence behavior;
5. administrator and independent inspection proving diagnostics are useful and contain no policy
   tenant identifiers or protected values;
6. independent security review with no unresolved critical/high findings; and
7. repetition against the exact trusted-signed GA candidate.

No item in this record passes those external gates.
