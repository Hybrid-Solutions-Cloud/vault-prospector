# Version 0.3 Preview scope

Version [`0.3.0-preview.6`](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/tag/v0.3.0-preview.6)
is the current unsigned Windows desktop Preview for non-production evaluation. It replaces withdrawn
`0.3.0-preview.2` with the complete approved C · Atlas startup and secure-unlock hierarchy,
including policy-controlled current-account verification for supported Remote Desktop and AVD
sessions. It retains the first-run and desktop-verification corrections from the 0.2 line.
`0.1.0-preview.2` also remains withdrawn and must not be installed or resubmitted.

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
- Preview browser-fill/native-host boundaries with explicit origin and field mappings, one-time
  desktop confirmation, and no browser credential-database access.
- Machine-managed enterprise policy for allowed tenants, providers, identity types, clipboard,
  and offline-cache behavior, including packaged ADMX/ADML templates.
- GitHub Actions validation on the HCS Azure runner, with Windows package validation routed through
  the ephemeral HCS Tier-4 Windows build VM.
- Five release packages, adjacent SHA-256 files, an SPDX SBOM, and five keyless Sigstore
  verification bundles.

## Future roadmap source not included in the Windows release contract

- CyberArk Privilege Cloud source and automated tests remain in the private repository for future
  development, but the CyberArk UI is disabled in the Windows release. No live-tenant support or
  GA evidence is claimed.
- Native iOS and Android prototypes remain future, independently gated products. Mobile binaries,
  signing, and store acceptance do not block the Windows release.

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
- Use only synthetic or non-production Azure resources. Governed live Azure, multi-tenant,
  Conditional Access, permission-failure, rotation, and revocation matrices remain open.
- Independent security, privacy/legal, accessibility, and representative usability approval are
  not complete.
- Browser integrations are validation-preview features, not store-approved production
  extensions. They do not import, export, scrape, or synchronize browser credentials.
- The iOS, Android, and CyberArk implementations are future roadmap work and are not part of this
  release's acceptance boundary.
- There is no supported cross-device DPAPI key migration. Reconnect identities and resynchronize
  from Azure on a replacement Windows profile or device.
- Package-manager availability follows external moderation and can lag the direct release.
- Project-controlled telemetry remains disabled. Feedback is voluntary and must not contain
  credentials, tokens, secret values, or sensitive identifiers.

The [release-readiness matrix](release-readiness.md) is authoritative for remaining Preview and GA
gates. Azure remains the system of record, and no release claim expands a user's existing
authorization.
