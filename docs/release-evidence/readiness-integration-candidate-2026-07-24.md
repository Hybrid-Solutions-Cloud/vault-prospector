# Readiness integration candidate — 2026-07-24

## Scope and truth boundary

Source commit `1185747307c6f0ca6b916abfbfc29cb16b125d4b` combines the completed
.NET 10 LTS, performance/scale, operational-readiness, legal/privacy, and
machine-managed enterprise-policy slices on `integration/readiness-candidate`.
It also retains the Phase 13 merge-evidence correction.

This is exact local source and disposable package evidence. It is not a trusted-signed public
candidate, a clean-machine installed lifecycle, hosted CI, independent security/legal approval,
representative-user or assistive-technology evidence, live Azure/CyberArk validation, physical
mobile-device validation, store acceptance, or GA approval.

## Integration defects corrected

Combining the independently developed slices exposed three fail-closed contract failures:

1. The performance probe still targeted `net9.0`; it now targets `net10.0` and its committed lock
   describes the .NET 10 graph.
2. The operational contract still expected desktop .NET 9 and its 2026 support deadline; it now
   verifies both desktop and mobile SDK major 10 against the official .NET 10 LTS
   `2028-11-14` end-of-support date.
3. The legal inventory still described the pre-integration lock graph; deterministic regeneration
   reduced it from 236 to 225 package/version records while retaining the explicit
   `AvaloniaUI.DiagnosticsSupport 2.2.3` approval-required finding.

CI now validates packaged legal/privacy files, and TRX logging no longer assigns one shared
filename that parallel test projects overwrite.

## Exact-source verification

Using pinned SDK `10.0.302`:

- locked solution restore passed;
- formatting verification passed;
- the Release solution build passed with 0 warnings and 0 errors;
- all 370 desktop/shared tests passed:
  Domain 4, Application 76, Infrastructure 56, CyberArk 12, Security 1, Platform 61,
  Azure 29, BrowserProtocol 36, App 87, and BrowserHost 8;
- structured direct/transitive NuGet vulnerability inspection found no known vulnerable package;
- browser-extension source tests passed 6/6 and its production build passed;
- managed mobile tests passed 44/44;
- Android arm64 Release AOT/linking/App Bundle production passed with 0 warnings and 0 errors;
- Windows-hosted iOS application/credential-provider reference-pack compilation passed; and
- full-history gitleaks scanned 141 commits and found no leaks.

## Readiness contracts

- Performance/scale passed all eight limits with 50,000 encrypted metadata objects:
  initialization 355 ms, synchronization 6,944 ms, reopen 1,375 ms, search p95 311 ms,
  search maximum 348 ms, cancellation 6 ms, private memory 45.6 MiB, and encrypted database
  24.6 MiB.
- Operational readiness passed 35/35 checks, including all three live public endpoints, with no
  warning or error.
- Legal/privacy source readiness passed 25/25 over 225 inventory records.
- Enterprise-policy source readiness passed 42/42; no live machine policy was present and the
  validator made no policy change.

## Disposable Windows candidate

Version `0.1.0-ci.940` was built from exact source commit `1185747`:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| MSI | 67,288,096 | `1E67B6267CA7A69EC6A529A185A9364512D968714F3AC323B1002F538D6C97AB` |
| ZIP | 104,518,274 | `16B7168D6EC0E5D7E54DCC2D3B447F731B133CB067C626A28B50F90D96F77EFC` |

The self-contained runtime reports `net10.0` and `Microsoft.NETCore.App 10.0.10`. The packaged
application remained alive for a five-second launch smoke test. Validation also passed:

- rollback-safe `RemoveExistingProducts` scheduling;
- Start-menu shortcut and icon;
- browser native-host payload, three registrations, extension identities, and disabled default
  browser-fill policy;
- packaged legal/privacy readiness 29/29;
- packaged enterprise-policy readiness 44/44;
- MSI File-table presence for `LICENSE.txt`, `PRIVACY.md`, `THIRD-PARTY-NOTICES.md`,
  `VaultProspector.admx`, and `VaultProspector.adml`;
- Chocolatey package creation; and
- generated WinGet manifest validation.

The disposable artifacts remain ignored local output and were not published.

## Remaining mandatory gates

- Required GitHub jobs must execute and pass on the final PR head and merge commit. Organization
  jobs are currently rejected before step 1 by the account payment/spending-limit condition.
- Run the exact trusted-signed candidate through clean supported-Windows install, upgrade,
  rollback, repair, uninstall, and package-manager lifecycle matrices.
- Complete governed Group Policy/Intune deployment and live Azure/CyberArk allowed/denied matrices.
- Complete independent security review, legal/privacy approval, public privacy publication,
  upstream-obligation review, signing-key lifecycle approval, usability/accessibility studies,
  physical-device and store validation, operational exercises, and the required stability windows.

No local result in this record closes those external gates.
