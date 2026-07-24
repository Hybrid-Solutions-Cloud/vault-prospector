# Version 0.2 Preview Scope

Version [`0.2.0-preview.1`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.2.0-preview.1)
is the current unsigned Windows desktop Preview for non-production evaluation. It supersedes
`0.1.1-preview.1`; `0.1.0-preview.2` remains withdrawn and must not be installed or resubmitted.

## Included

- Windows x64 MSI and portable self-contained ZIP.
- Validated WinGet manifests and Chocolatey package for community-repository submission.
- Multi-account MSAL public-client authentication with app-owned cache isolation.
- Interactive, managed-identity, certificate, and workload-federation profile models.
- Subscription and Azure Key Vault discovery plus metadata-only indexing for secrets, keys, and
  certificates.
- SQLCipher-encrypted local search, workspaces, favorites, access history, filters, and cancelable
  partial synchronization.
- Explicit Windows Hello-gated reveal/copy and optional DPAPI/AES-GCM protected offline values.
- Local-data recovery, rotation, purge, and fail-closed corruption/tamper handling.
- Read-only workload authorization assessment and deterministic provisioning previews; this
  release does not execute identity, RBAC, or Key Vault writes.
- Preview CyberArk Privilege Cloud metadata discovery and verified explicit retrieval with
  separate DPAPI-protected provider credentials.
- Preview browser-fill/native-host boundaries with explicit origin and field mappings, one-time
  desktop confirmation, and no browser credential-database access.
- Machine-managed enterprise policy for allowed tenants, providers, identity types, clipboard,
  and offline-cache behavior, including packaged ADMX/ADML templates.
- Native iOS and Android prototypes and fail-closed credential/autofill extension boundaries in
  source and CI. Mobile binaries are not distributed by this Windows release.
- ADO CI with 370 Windows/shared tests, 44 managed mobile tests, native iOS simulator builds,
  Android Release App Bundle packaging, dependency and secret scanning, package validation, and
  operational/legal/enterprise gates.
- Four release packages, adjacent SHA-256 files, SPDX SBOM, and four Key Vault-backed Cosign
  verification bundles.

## Distribution status

- Direct public MSI/ZIP/NUPKG/WinGet-bundle download: available.
- WinGet: validated and submitted in
  [`microsoft/winget-pkgs#407541`](https://github.com/microsoft/winget-pkgs/pull/407541);
  catalog acceptance is pending.
- Chocolatey: the exact package was submitted twice, but both uploads returned HTTP 504 and the
  package is not present in the catalog.

## Preview limitations

- Windows binaries are not Authenticode-signed. Windows displays **Unknown Publisher**. Package
  hashes, SBOM, and Cosign bundles provide integrity/provenance evidence but do not replace trusted
  Windows code signing.
- Use only synthetic or non-production Azure and CyberArk resources. Governed live-provider,
  multi-tenant, Conditional Access, permission-failure, rotation, and revocation matrices remain
  open.
- Independent security, privacy/legal, accessibility, and representative usability approval are
  not complete.
- Browser integrations are validation-preview features, not store-approved production
  extensions. They do not import, export, scrape, or synchronize browser credentials.
- The iOS and Android applications are prototypes only. Physical-device security/accessibility,
  protected signing, TestFlight/Play closed testing, store declarations, and store acceptance
  remain open.
- There is no supported cross-device DPAPI key migration. Reconnect identities and resynchronize
  from Azure on a replacement Windows profile or device.
- Package-manager availability follows external moderation and can lag the direct release.
- Project-controlled telemetry remains disabled. Feedback is voluntary and must not contain
  credentials, tokens, secret values, or sensitive identifiers.

The [release-readiness matrix](release-readiness.md) is authoritative for remaining Preview and GA
gates. Azure and CyberArk remain their respective systems of record, and no release claim expands
a user's existing authorization.
