# G-09 legal and privacy review

**Status:** In progress — source controls implemented; approval absent  
**Review date:** 2026-07-24  
**Prepared by:** Codex under HCS governance

## Source review completed

- The repository carries the MIT license.
- A deterministic inventory covers 236 exact NuGet/npm package-version records from committed
  production, build, test, mobile, and design-prototype lock files.
- Generated `THIRD-PARTY-NOTICES.md` conservatively lists production lock-graph components and is
  packaged beside `LICENSE.txt` and `PRIVACY.md` in Windows ZIP/MSI payloads.
- The technical privacy statement documents identity/token/metadata/value/clipboard/log/settings/
  recovery/browser/CyberArk/mobile handling, default retention, network activity, deletion, and the
  no-project-telemetry boundary.
- Production project files contain no Application Insights, OpenTelemetry, Sentry, or App Center
  package reference. Application Insights appears transitively only in test tooling; it is not a
  shipped application dependency or product telemetry implementation.
- WinGet, Chocolatey, Apple, and Google package/store fields and open declarations are recorded in
  [Package and store metadata](package-and-store-metadata.md).
- CI validates inventory drift, disclosures, production telemetry-package absence, platform
  privacy defaults, and package-notice copying.

## Open review findings

| ID | Finding | Required closure |
| --- | --- | --- |
| L-01 | `AvaloniaUI.DiagnosticsSupport 2.2.3` declares no license in its NuGet metadata. Release builds exclude its assets, but developer-use terms remain unapproved. | Obtain and record authoritative terms or remove/replace the Debug-only package. |
| L-02 | Package metadata is not a legal opinion and upstream copyright/NOTICE obligations have not received human review. | Reviewer reconciles exact-candidate SBOM, package files, upstream licenses, notices, and redistribution terms. |
| L-03 | The public release repository has no approved privacy-policy URL. | Approve and publish the privacy statement at a stable public HCS URL, then update every package/store listing. |
| L-04 | The committed inventory is lock-graph based and deliberately conservative; it does not prove exact files distributed. | Reconcile the exact signed MSI/ZIP/AAB/IPA SBOMs and file manifests. |
| L-05 | Apple privacy answers, required-reason APIs, export classification, age/category, entitlements, and extension disclosure are drafts. | Review the exact signed TestFlight build and observed traffic; record App Store Connect answers and approval. |
| L-06 | Google Play Data safety, data/account deletion, content rating, target SDK, and SDK disclosure answers are drafts. | Review the exact signed closed-test AAB and observed traffic; record Play Console answers and approval. |
| L-07 | No named legal/privacy approver has signed the product, package, or store statements. | Record named approver, date, artifact hashes, accepted exceptions, and corrective dates. |

## Approval record

| Role | Name | Decision | Date | Exact evidence |
| --- | --- | --- | --- | --- |
| Product owner | Not recorded | Pending | — | — |
| Legal/privacy reviewer | Not recorded | Pending | — | — |
| Mobile store declaration reviewer | Not recorded | Pending | — | — |

G-09 remains **In progress**. Automated checks prove source consistency and disclosed gaps; they
cannot provide legal approval.
