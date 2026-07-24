# Azure Artifact Signing Setup

Vault Prospector stable and GA Windows releases must use Microsoft-trusted Authenticode signatures
and RFC 3161 timestamps. An explicitly labeled non-production Preview evaluation may be unsigned
when its release page warns about Unknown Publisher and retains checksums, SBOM, Sigstore, and
provenance. Sigstore does not replace Windows publisher trust.

## Current state

As of 2026-07-16, an HCS-governed Azure resource inventory query found no
`Microsoft.CodeSigning/codeSigningAccounts` resource in the HCS management subscription. The
release pipeline therefore fails closed for stable and GA tags when Artifact Signing configuration
is absent. Only a tag matching
`vX.Y.Z-preview.N` may take the explicit unsigned evaluation path.

The intended trust model is **Public Trust**. Azure recommends Public Trust for publicly
distributed Win32 applications. Do not substitute a Public Trust Test or Private Trust profile for
a signed release.

## One-time Azure and Azure DevOps setup

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
6. Install Microsoft's Artifact Signing Azure DevOps extension in the HCS organization.
7. Create a least-privilege workload-identity-federated Azure service connection for the
   `Vault Prospector` ADO project.
8. Assign that service principal **Artifact Signing Certificate Profile Signer** on the certificate
   profile resource only. Do not assign subscription Owner or Contributor to the release identity.
9. Configure these non-secret variables for the protected ADO release pipeline:

   | Variable | Value |
   | --- | --- |
   | `ARTIFACT_SIGNING_CLIENT_ID` | Federated release application's client ID |
   | `ARTIFACT_SIGNING_TENANT_ID` | HCS tenant ID |
   | `ARTIFACT_SIGNING_SUBSCRIPTION_ID` | Subscription containing the signing account |
   | `ARTIFACT_SIGNING_ENDPOINT` | Regional endpoint shown by the signing account, including `https://` |
   | `ARTIFACT_SIGNING_ACCOUNT` | Artifact Signing account name |
   | `ARTIFACT_SIGNING_PROFILE` | Public Trust certificate profile name |

No PFX, certificate password, Azure client secret, or exportable signing key belongs in a pipeline
variable. Azure DevOps exchanges its workload-identity assertion for Azure access, and Azure keeps
the signing key in the managed service.

## Workflow behavior

For every version tag, `.ado/release.yml`:

1. requires all signing variables for stable/GA, while permitting only an explicitly versioned
   Preview evaluation to proceed unsigned;
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
