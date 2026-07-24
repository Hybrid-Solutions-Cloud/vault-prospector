# ADR-0012: Isolate and rotate workload credentials

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner and maintainers

## Context

Vault Prospector supports human interactive identities, Azure managed identities, certificate
service principals, and federated service principals. A desktop public client cannot safely retain
a client secret. Human MSAL caches, host-managed identity endpoints, certificates, and projected
OIDC tokens also have different ownership and revocation semantics.

## Decision

- Human identities use only the app-owned MSAL public-client cache.
- Managed identities use only the detected Azure host endpoint. Vault Prospector stores no managed
  identity credential and never offers this profile on an ordinary host.
- Certificate service principals store the normalized certificate thumbprint in the encrypted
  metadata database. The private key remains in the Windows certificate store.
- Federated service principals store only the canonical projected-token file path in the encrypted
  metadata database. Token content remains in the issuer-managed file and is never copied to the
  database, settings, diagnostics, or a human token cache.
- Client secrets remain unsupported.
- A replacement certificate thumbprint or token-file path is normalized and used to acquire an ARM
  token before it replaces the persisted profile. Validation failure leaves the previous profile
  unchanged.
- Local revocation first persists a disabled `Revoked` state and removes the stored workload
  credential reference. It then removes app-owned human tokens when applicable. Online application
  services re-read and enforce persisted identity state so a stale UI object cannot bypass
  revocation.
- Local revocation purges offline copies for vaults discovered through that identity. The user must
  also revoke a compromised certificate, federated trust, or managed-identity assignment at its
  external issuer; Vault Prospector cannot claim to revoke credentials it does not own.
- Lifecycle diagnostics contain only a pseudonymized identity identifier, fixed identity-type enum,
  fixed outcome, event name, and exception type.

## Consequences

- Workload credentials cannot silently inherit Azure CLI, Azure PowerShell, IDE, terminal, or human
  MSAL state.
- Projected tokens can rotate in place under issuer control without the application persisting
  token content.
- A revoked workload profile needs a validated replacement credential before it can become ready.
- External issuer revocation, live Azure validation, and independent security review remain
  operational release requirements.

## References

- [Microsoft WorkloadIdentityCredential constructor](https://learn.microsoft.com/dotnet/api/azure.identity.workloadidentitycredential.-ctor)
- [Microsoft WorkloadIdentityCredentialOptions.TokenFilePath](https://learn.microsoft.com/dotnet/api/azure.identity.workloadidentitycredentialoptions.tokenfilepath)
