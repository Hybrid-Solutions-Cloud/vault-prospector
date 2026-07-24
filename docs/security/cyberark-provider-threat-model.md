# CyberArk Privilege Cloud provider threat model

**Status:** Internal design-review draft; independent review and live tenant evidence required

**Date:** 2026-07-24

**Related decision:** [ADR-0015](../adr/0015-cyberark-privilege-cloud-provider.md)

## Security objective

A CyberArk connection, metadata response, local profile, or UI action must not disclose a provider
credential, platform token, or account value outside the exact enabled profile and explicitly
selected account/version. Metadata synchronization must never retrieve values. Every value request
requires fresh Windows verification and value-free audit.

## Protected assets

- CyberArk Identity service-user client credentials;
- Identity access tokens and Privilege Cloud platform tokens;
- CyberArk account values and selected historic versions;
- profile, safe, account, permission, and version metadata;
- service-user and tenant authorization boundaries;
- Windows verification and foreground-lock state; and
- local and CyberArk server-side audit evidence.

## Trust boundaries

1. user input to the profile editor;
2. UI string state to the DPAPI credential store;
3. current Windows account to DPAPI-protected credential files;
4. SQLCipher metadata to the provider-specific application service;
5. application to CyberArk Identity over TLS;
6. Identity authorization redirect to the short-lived platform token parser;
7. application to Privilege Cloud REST endpoints over TLS;
8. CyberArk JSON and pagination links to bounded provider models; and
9. explicit account selection and Windows verification to value presentation or clipboard.

## Required invariants

- Only root HTTPS URLs on the selected CyberArk production domains are accepted. User info, query,
  fragment, custom ports, custom domains, and redirects are rejected.
- The HTTP handler does not follow redirects. The only accepted Identity authorization location is
  the exact CyberArk redirect URI carrying one bounded `id_token`.
- Authentication tokens live for one validate, sync, or retrieve operation and are never persisted
  or logged.
- A client credential is validated before DPAPI persistence or replacement. A failed validation
  leaves the prior profile and credential intact.
- Credential files are keyed by canonical profile GUID, protected for `CurrentUser`, use
  profile-specific entropy, and cannot be replayed under another profile.
- SQLCipher metadata never contains a client credential, Identity token, platform token, retrieval
  reason, or account value.
- Provider responses and pagination are bounded by bytes, pages, items, origin, API path, and
  cancellation.
- Sync calls safe, member, account, and version metadata endpoints only; it never calls
  `Password/Retrieve`.
- Retrieval rehydrates the exact profile and account, requires `Ready`, requires a non-empty
  business reason, and permits only `show` or `copy`.
- Every Windows verification outcome except `Verified` denies before the provider request.
- An authorization audit must commit before retrieval. A returned value is disposed if the
  success audit cannot commit.
- Lock, background, system-boundary, cancellation, and presentation-epoch changes hide values and
  prevent late presentation.
- Local permission evidence is labeled direct observation. It never overclaims effective access
  through groups, roles, confirmation, tickets, or platform policy.

## Threats, controls, and evidence

| Threat | Control | Evidence |
| --- | --- | --- |
| Credential written to metadata or logs | Separate DPAPI store; no credential fields in schema; safe exception text | SQLCipher/DPAPI plaintext canaries, redaction tests, static secret scan |
| Credential replay under another profile | Profile-specific DPAPI entropy and canonical GUID filename | Cross-profile replay test |
| Malicious endpoint receives credentials | Exact HTTPS CyberArk suffix, root path/default port, no user info/query/fragment | Unsupported-domain/port/path tests |
| Redirect exfiltrates token | Redirects disabled; final request origin checked; exact authorization redirect allowlist | Untrusted redirect tests and live proxy capture |
| Pagination pivots to attacker | Same origin plus `/PasswordVault/API/` path and no fragment/user info | Untrusted `nextLink` test |
| Oversized/malformed response exhausts or confuses client | 4 MiB metadata/1 MiB value limits, strict required fields, page/item/version caps | Oversize, malformed JSON, missing field, page/item limit tests |
| Sync silently retrieves values | Separate discovery methods with no retrieval route | Request-sequence contract tests and live traffic capture |
| Disabled/stale profile retrieves | `Ready` and enabled rehydration immediately before verification/provider call | disabled, re-enabled-unvalidated, removed, and revoked tests |
| User selects account from another profile | Profile/account GUID equality enforced in provider and persistence transaction | cross-profile account/snapshot tests |
| Verification bypass | Fresh Windows verification; all non-verified outcomes deny | result matrix and UI workflow tests |
| Audit failure loses accountability | authorization audit before request; value disposal when result audit fails | injected audit failure tests |
| Server error leaks response body | Status/category-only exception; response bytes zeroed; no body in diagnostics | credential/body canary tests |
| Value persists after presentation | disposable `SensitiveValue`, ten-second presentation epoch, clipboard owner-clear policy, boundary lock | cancellation, lock/background, timer, clipboard tests |
| Direct member evidence overclaims access | Explicit evidence label; provider remains authoritative | group/role/confirmation/ticketing live matrix |
| Credential removal leaves usable source | local revoke persists disabled/revoked state before deleting the protected credential; profile removal deletes credential before metadata | fail-closed removal tests, restart tests, and external service-user revocation drill |

## Explicitly prohibited behavior

- storing client credentials, access tokens, platform tokens, reasons, or values in SQLCipher;
- logging HTTP headers, bodies, provider exception bodies, account values, or client credentials;
- using Azure identities, Azure token caches, Azure provider contracts, or browser mappings for
  CyberArk;
- retrieving values during validation, search, background activity, or metadata sync;
- following arbitrary redirects or pagination links;
- accepting on-premises/custom CyberArk endpoints under the Privilege Cloud support label;
- treating direct safe-member evidence as complete effective authorization;
- automatic retry of value retrieval;
- offline caching of CyberArk values in the initial implementation; and
- claiming supported release before live tenant and independent-review gates pass.

## Credential compromise and revocation

1. Disable the local profile to block retrieval immediately.
2. Revoke or rotate the service-user credential in CyberArk Identity; local removal alone is not
   external revocation.
3. Remove the profile to delete the DPAPI credential and synchronized metadata while preserving
   value-free local audit.
4. Review CyberArk authoritative audit, local audit, service-user role/safe membership, and
   relevant account access.
5. Rotate affected account values using CyberArk-authorized operations outside Vault Prospector
   when exposure is plausible.
6. Reconnect only with a least-privilege replacement credential after incident disposition.

## Release gates

- accepted ADR and closed internal threat-model findings;
- governed non-production Privilege Cloud tenant contract and least-privilege matrix;
- service-user create/rotate/revoke/remove and token-expiry evidence;
- pagination, throttling, partial failure, confirmation, ticketing, dual-control, and version
  retrieval evidence;
- packet/log/crash-dump review showing no credential, token, reason, or value leakage;
- Windows DPAPI, reinstall, upgrade, recovery, lock, clipboard, and accessibility validation;
- independent security review with no open critical/high findings; and
- exact signed installer and release-artifact validation.
