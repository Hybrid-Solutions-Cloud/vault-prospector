# Readiness integration candidate — 2026-07-24

## Scope and truth boundary

Source commit `fc20f58973b541195cf49141ae8a74a03d9681da` combines the completed
.NET 10 LTS, performance/scale, operational-readiness, legal/privacy, and
machine-managed enterprise-policy slices on `integration/readiness-candidate`.
It also retains the Phase 13 merge-evidence correction and migrates all delivery automation to
the HCS Azure DevOps project.

This is exact local source, disposable package, and Azure Pipelines hosted evidence. It is not a
trusted-signed public candidate, a clean-machine installed lifecycle, independent security/legal approval,
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
filename that parallel test projects overwrite. Hosted validation also exposed and corrected:

4. Azure Pipelines service identities could not execute the AppX-owned WinGet binary directly
   from `WindowsApps`; the verified installed payload is now copied to agent temp before manifest
   validation.
5. Intel macOS agents correctly selected `iossimulator-x64`, but locked restore contained only
   Apple-silicon simulator assets; both the application and credential-provider extension now
   include locked Intel simulator targets.

## Exact-source verification

Using pinned SDK `10.0.302`:

- Azure Pipelines build
  [`281`](https://dev.azure.com/hybridcloudsolutions/51cf361f-78a7-4a0d-8804-25cb4887361b/_build/results?buildId=281)
  passed all four jobs against PR merge commit
  `c39270f62537f34c1094213b76a20a93e74e1598`;
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
- full-history Gitleaks scanned 150 commits and found no leaks.

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

Azure Pipelines version `0.1.0-ci.281` was built from the exact PR merge commit. The MSI SHA-256 is
`CDCADBB18C81DBC056CE6728DF8008B9F92C014EDB00D61C239A9ABA1C7283BF`; the pipeline retained the
candidate, provenance, and test evidence as build artifacts.

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

- Merge PR #22 and require the ADO CI definition to pass again on the exact `main` merge commit.
- Run the ADO release definition on the immutable Preview tag and verify the public release,
  checksums, SPDX SBOM, Sigstore bundles, and package-manager submission.
- Run the exact trusted-signed candidate through clean supported-Windows install, upgrade,
  rollback, repair, uninstall, and package-manager lifecycle matrices.
- Complete governed Group Policy/Intune deployment and live Azure/CyberArk allowed/denied matrices.
- Complete independent security review, legal/privacy approval, public privacy publication,
  upstream-obligation review, signing-key lifecycle approval, usability/accessibility studies,
  physical-device and store validation, operational exercises, and the required stability windows.

No local result in this record closes those external gates.

## Initial hosted PR state

PR [#22](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/pull/22) opened from evidence
head `4427f48b8a8d90594f74bba5b8dde509cbc11dd9`. CI run
[`30099139189`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30099139189)
created `build-test` and `secret-scan`; Mobile CI run
[`30099139278`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/actions/runs/30099139278)
created `managed-tests`, `android-package`, and `ios-simulator`.

All five jobs completed as failures with zero steps. Each check-run annotation states that the job
was not started because recent account payments failed or the spending limit must be increased.
This was an external hosted-startup block, not a code-test result.

## Final hosted PR state

HCS delivery automation now uses Azure DevOps exclusively:

- private project: `Vault Prospector`;
- CI definition: `Vault Prospector CI` (definition `5`);
- scheduled readiness definition: `Vault Prospector Operational Readiness` (definition `6`);
- release definition: `Vault Prospector Release` (definition `7`);
- GitHub App service connection: `Hybrid-Solutions-Cloud GitHub`;
- Key Vault-linked release variable group: `vp-prd-secrets`; and
- release package signing key: `hcs-vault-prospector-release-signing-key` in
  `kv-hcs-vault-01`.

Exact PR validation build
[`281`](https://dev.azure.com/hybridcloudsolutions/51cf361f-78a7-4a0d-8804-25cb4887361b/_build/results?buildId=281)
ran from `2026-07-24T19:21:51Z` through `2026-07-24T19:42:15Z` and passed:

- Windows build, all 370 tests, performance, browser, PowerShell, MSI/ZIP/package-manager
  validation, operational/legal/policy gates, and artifact retention;
- full-history secret scan;
- native iOS simulator application and embedded credential-provider extension compilation on
  Xcode 26.0.1; and
- all 44 managed mobile tests plus the Android Release App Bundle with zero warnings and errors.

GitHub reports the ADO `Vault Prospector CI` status on PR #22. The obsolete GitHub Actions workflow
definitions were removed so the repository does not mix CI/CD systems.
