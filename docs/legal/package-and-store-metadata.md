# Package and store metadata baseline

**Status:** Draft; not legally approved and not submitted  
**Owner:** Kristopher Turner / Hybrid Solutions Cloud  
**Last reconciled:** 2026-07-24

## Common product identity

| Field | Canonical value |
| --- | --- |
| Product | Vault Prospector |
| Publisher | Hybrid Solutions Cloud |
| Source license | MIT |
| Source license file | `LICENSE` |
| Technical privacy statement | `docs/privacy.md` |
| Public support instructions | <https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/blob/main/FEEDBACK.md> |
| Private vulnerability reporting | <kris@hybridsolutions.cloud> as defined in `SECURITY.md` |
| Product telemetry | Disabled |
| Production support | None before GA |

The public release repository does not yet contain a privacy-policy page. Mobile submission and any
package catalog requiring a public privacy URL are blocked until the approved statement is mirrored
to an immutable, publicly reachable HCS-controlled URL.

## Windows distribution metadata

| Field | Value |
| --- | --- |
| WinGet identifier | `HybridSolutionsCloud.VaultProspector` |
| Chocolatey identifier | `vault-prospector` |
| Architecture | Windows x64 |
| Installer | Machine-scope WiX MSI |
| Short description | Securely discover and search Azure Key Vault metadata across Microsoft Entra identities. |
| Long description | Vault Prospector is a local-first Windows application for encrypted Azure Key Vault metadata discovery, offline search, and explicit Windows Hello-gated secret retrieval. |
| Tags | `azure`, `key-vault`, `security`, `secrets`, `windows` |
| License | MIT |

`scripts/PackageDistribution.ps1` is the production source for generated WinGet and Chocolatey
metadata. Each exact release still requires moderation-policy review, public URL/hash agreement,
license/notice presence, SBOM reconciliation, and installation verification.

## Apple App Store draft

| Field | Draft value |
| --- | --- |
| App name | Vault Prospector |
| Bundle identifier | `cloud.hybridsolutions.vaultprospector` |
| Extension identifier | `cloud.hybridsolutions.vaultprospector.credentialprovider` |
| Minimum OS | iOS/iPadOS 18 |
| Category | Developer Tools or Utilities; final selection requires product-owner review |
| Tracking | No project-controlled tracking |
| Developer-collected data | Draft says none; must be reconciled against the exact signed app, MSAL/Azure SDK disclosures, and observed traffic |
| Advertising | None |
| Privacy manifest | Embedded `PrivacyInfo.xcprivacy`; declares no tracking or collected-data types |
| Encryption declaration | `ITSAppUsesNonExemptEncryption=false`; export-compliance review is still required |
| Autofill | Disabled feasibility extension; no credential value delivery until its separate gates pass |

The Face ID usage string describes local unlock and explicit secret access. Final App Privacy
answers, age rating, category, screenshots, export classification, entitlement disclosure, and
extension description require review against the signed TestFlight candidate.

## Google Play draft

| Field | Draft value |
| --- | --- |
| App name | Vault Prospector |
| Application identifier | `cloud.hybridsolutions.vaultprospector` |
| Minimum API | 31 |
| Package | Android App Bundle |
| Tracking/advertising | None |
| Developer-collected data | Draft says none; must be reconciled against the exact signed AAB, MSAL/Azure SDK disclosures, and observed traffic |
| Backup/transfer | Disabled |
| Cleartext traffic | Disabled |
| Permissions | `INTERNET`, `USE_BIOMETRIC` |
| Autofill | Package-disabled feasibility service; no dataset/value delivery |

Final Data safety, content rating, target-SDK compliance, privacy URL, account/data deletion answers,
store graphics, closed-test evidence, and reviewer instructions remain unapproved.

## Approval requirements

Before G-09 or either mobile store gate passes:

1. legal/privacy reviewers approve the MIT and third-party notice treatment;
2. the exact candidate SBOM is reconciled to packaged files and upstream license/NOTICE obligations;
3. an approved public privacy URL is published and used consistently;
4. Apple privacy/export and Google Play Data safety answers are checked against observed signed-app
   traffic and all transitive SDK disclosures; and
5. the approver, date, exact source/artifact hashes, exceptions, and corrective dates are recorded.
