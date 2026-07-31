---
layout: home

hero:
  name: Vault Prospector
  text: Find any Azure Key Vault secret, across every tenant
  tagline: A local-first Windows desktop app that indexes Key Vault metadata across all of your Microsoft Entra identities, tenants, and subscriptions — and never reveals a value without an explicit action and a Windows Hello check.
  actions:
    - theme: brand
      text: Download the Preview
      link: /downloads
    - theme: alt
      text: Read the user guide
      link: /user-guide
    - theme: alt
      text: Roadmap
      link: /product/roadmap

features:
  - title: Search offline, instantly
    details: Metadata is indexed into a SQLCipher-encrypted local database. Search by name and tag across every vault you can reach, with filters for identity, tenant, subscription, type, expiration, and staleness — with no network round trip.
  - title: Values stay put until you ask
    details: Secret, key, and certificate values are never indexed. Retrieval is an explicit action, the display is masked, Windows Hello verification is required, and the clipboard clears on a timer.
  - title: Many identities, kept apart
    details: Sign in with as many Entra accounts as you need. Each identity gets an isolated token-cache entry, with full MFA and Conditional Access support.
  - title: Nothing phones home
    details: No telemetry. Local diagnostics are redacted — no tokens, secret values, usernames, vault names, or object names. Vault Prospector never creates role assignments, rotates secrets, or exports private keys.
---

## ⚠️ This is a major work in progress

**Vault Prospector is Preview software under active development. Do not use it in production.**

Read this before you download anything:

- The current release is **`0.3.0-preview.3`** — a **preview**, published for non-production evaluation only.
- **Direct packages are unsigned.** Windows will display **Unknown Publisher** when you run the installer. You must [verify the published SHA-256 and Sigstore bundle](/release) before installing. A trusted, signed channel via the Microsoft Store is planned but not yet available.
- Features land, change shape, and get replaced between previews. Expect breaking changes to the UI, the local database, and configuration between releases.
- **CyberArk support and the native mobile apps are not implemented.** They exist as future-roadmap source in this repository and are not part of any current release.
- Enterprise policy and browser-fill are themselves marked Preview inside a Preview.
- Documentation on this site is being written alongside the product and will be incomplete in places.

If you hit a problem, [file feedback publicly](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/blob/main/FEEDBACK.md) — but report security issues **privately** per [SECURITY.md](https://github.com/Hybrid-Solutions-Cloud/vault-prospector/blob/main/SECURITY.md). Never include credentials, tokens, secret values, or vault names in any report.

## Download

Current Preview: **`0.3.0-preview.3`**

- [**Windows installer (MSI)**](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.msi) — start here
- [**Portable ZIP**](https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases/download/v0.3.0-preview.3/VaultProspector-0.3.0-preview.3-win-x64.zip) — no installer required
- [**All downloads**](/downloads) — MSIX, Chocolatey, WinGet manifests, checksums, signatures, SBOM

**Verify before you install** — these packages are unsigned. The [release verification guide](/release) covers the checksum and Sigstore steps.

## Requirements

- Windows 10/11 x64, with Windows Hello configured for verification prompts.
- A Microsoft Entra account with read access to the Key Vaults you want to index.

Building from source additionally needs PowerShell 7+ and the .NET SDK pinned in `global.json`.

## Where to go next

- **[Install and verify a release](/release)** — download, checksum, Sigstore, first run.
- **[Authentication setup](/authentication)** — consent, custom registrations, multiple identities.
- **[User guide](/user-guide)** — day-to-day search, sync, reveal, and workspaces.
- **[Enterprise policy](/enterprise-policy)** — machine-managed restrictions and ADMX templates.
- **[Roadmap](/product/roadmap)** — what's planned and what's explicitly out of scope.
- **[Changelog](/changelog)** — everything that has shipped so far.
