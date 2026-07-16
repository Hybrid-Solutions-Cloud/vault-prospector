# Azure Artifact Signing Setup

Vault Prospector public Windows releases must use Microsoft-trusted Authenticode signatures and
RFC 3161 timestamps. Sigstore provenance remains additive; it does not replace Windows trust for
the executable and MSI.

## Current state

As of 2026-07-16, an HCS-governed Azure resource inventory query found no
`Microsoft.CodeSigning/codeSigningAccounts` resource in subscription
`be069ae1-fc96-4a07-9f8e-5994d83a137d`. The release workflow therefore fails closed before
building a tag when its Artifact Signing configuration is absent. It cannot publish an unsigned
replacement release.

The intended trust model is **Public Trust**. Azure recommends Public Trust for publicly
distributed Win32 applications. Do not substitute a Public Trust Test or Private Trust profile for
a public Preview or GA release.

## One-time Azure and GitHub setup

These steps create billable external resources and include a portal-only identity-validation
decision. They require the HCS owner or an explicitly authorized administrator:

1. Confirm the Azure billing profile exactly matches the legal person or organization that should
   appear as the software publisher.
2. Register the `Microsoft.CodeSigning` resource provider in the HCS subscription.
3. Create an Azure Artifact Signing account in an HCS-owned resource group and supported region.
4. In the Azure portal, assign the operator **Artifact Signing Identity Verifier**, submit Public
   Trust identity validation, and complete the emailed verification. Identity validation cannot be
   completed through CLI automation.
5. Create a **PublicTrust** certificate profile after validation succeeds.
6. Create a least-privilege Microsoft Entra application/service principal for GitHub Actions. Add
   a federated credential with this exact subject:

   ```text
   repo:Hybrid-Solutions-Cloud/vault-prospector:environment:release
   ```

7. Assign that service principal **Artifact Signing Certificate Profile Signer** on the certificate
   profile resource only. Do not assign subscription Owner or Contributor to the release identity.
8. In the GitHub `release` environment, configure these non-secret variables:

   | Variable | Value |
   | --- | --- |
   | `ARTIFACT_SIGNING_CLIENT_ID` | Federated release application's client ID |
   | `ARTIFACT_SIGNING_TENANT_ID` | HCS tenant ID |
   | `ARTIFACT_SIGNING_SUBSCRIPTION_ID` | Subscription containing the signing account |
   | `ARTIFACT_SIGNING_ENDPOINT` | Regional endpoint shown by the signing account, including `https://` |
   | `ARTIFACT_SIGNING_ACCOUNT` | Artifact Signing account name |
   | `ARTIFACT_SIGNING_PROFILE` | Public Trust certificate profile name |

No PFX, certificate password, Azure client secret, or long-lived signing key belongs in GitHub or
Key Vault for this workflow. GitHub exchanges its short-lived OIDC assertion for Azure access, and
Azure keeps the signing key in the managed service.

## Workflow behavior

For every version tag, `.github/workflows/release.yml`:

1. requires all signing variables and refuses an unsigned release;
2. builds and tests with locked dependencies and enforced vulnerability/secret gates;
3. publishes the Windows app, then signs and timestamps the Vault Prospector executables and
   assemblies before creating the portable ZIP;
4. builds the MSI from those signed files, then signs and timestamps the MSI;
5. verifies every expected Authenticode signature and timestamp;
6. recalculates the signed MSI checksum before generating WinGet and Chocolatey metadata;
7. produces SBOM, provenance, Sigstore bundles, hashes, and immutable release assets.

## Acceptance evidence

P-13 passes only after a fresh candidate workflow proves all of the following:

- the signer subject matches the approved HCS identity validation;
- `Get-AuthenticodeSignature` returns `Valid` for the MSI, application executable, and project
  assemblies, with a timestamp certificate present;
- Windows signature UI shows a valid chain on a clean supported Windows machine;
- MSI, WinGet, and Chocolatey install the exact signed hash recorded in release evidence;
- signature validation still succeeds after the short-lived signing certificate expires because
  the RFC 3161 timestamp remains valid;
- the release OIDC principal cannot manage the signing account, certificate profile, subscription,
  or unrelated Azure resources.

Record the account resource ID, profile resource ID, signer subject, workflow run URL, certificate
thumbprint, timestamp, and artifact hashes in the candidate release-evidence file. Do not record an
access token or private key.
