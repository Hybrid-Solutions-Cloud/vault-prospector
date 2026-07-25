# Trusted Windows Distribution Without Paid Signing

Vault Prospector uses the Microsoft Store as its free, publicly trusted Windows distribution path.
The release workflow creates an unsigned MSIX for Partner Center ingestion. Microsoft validates and
re-signs the package after certification, so the installed Store package has Microsoft-backed
publisher trust without an HCS-purchased code-signing certificate.

Direct MSI and portable ZIP downloads remain available for evaluation and enterprise-managed
deployment. They are explicitly unsigned and therefore display **Unknown Publisher**. SHA-256
checksums, SPDX SBOMs, immutable release assets, and Sigstore bundles protect integrity and
provenance, but they do not create Windows publisher trust.

## Why this is the selected path

- A self-signed certificate is not trusted on an unmanaged Windows system.
- A publicly trusted CA certificate and Azure Artifact Signing are paid services.
- Microsoft Store ingestion is free, accepts an unsigned package, and signs the package after
  certification.
- The source repository remains private; only versioned binaries and verification material are
  published in `Hybrid-Solutions-Cloud/vault-prospector-releases`.

No stable or GA claim may describe the direct MSI, ZIP, or pre-ingestion MSIX as Authenticode
trusted.

## Build the MSIX

Run:

```powershell
./scripts/PackageMsix.ps1 -Version 0.3.0-preview.1
```

The script:

1. publishes the self-contained Windows x64 application;
2. creates deterministic Store tile assets;
3. restores the pinned `Microsoft.Windows.SDK.BuildTools` package and verifies its SHA-256;
4. builds `artifacts/VaultProspector-<version>-win-x64.msix`; and
5. writes the adjacent SHA-256 file.

For Partner Center submission, reserve the application name and copy the exact package identity and
publisher values from **Product management > Product identity**:

```powershell
./scripts/PackageMsix.ps1 `
  -Version 0.3.0 `
  -IdentityName '<Partner Center package identity name>' `
  -Publisher '<Partner Center publisher subject>'
```

The values must match Partner Center exactly. The default development identity is suitable for
local packaging validation only.

## Release behavior

`.github/workflows/release.yml` runs on an ephemeral HCS Windows runner. It builds and tests the
exact tag, creates MSI, ZIP, MSIX, WinGet, and Chocolatey candidates, generates checksums and an
SPDX SBOM, produces keyless Sigstore bundles, and publishes binaries only to the public release
repository using the HCS GitHub App.

The public release must label the direct packages as unsigned. The Store-signed artifact or Store
listing URL is recorded separately after certification; Microsoft-signed bytes must never be
silently substituted under an existing tag.

## Acceptance evidence

The trusted Windows distribution gate passes when:

- the exact source tag produces a reproducible MSIX and adjacent SHA-256;
- Partner Center accepts the package identity and certification submission;
- Microsoft returns a signed package and/or live Store listing;
- a clean supported Windows system installs, launches, upgrades, and uninstalls the Store package;
- the Store package version maps to the immutable source tag and release evidence; and
- direct-download documentation continues to identify MSI, ZIP, and pre-ingestion MSIX files as
  unsigned.

No paid certificate, Azure Artifact Signing account, PFX, or exportable signing key is required by
this design.
