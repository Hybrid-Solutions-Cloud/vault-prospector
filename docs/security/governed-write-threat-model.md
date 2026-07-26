# Governed Azure mutation threat model

**Status:** Production implementation ready for review; default-disabled pending independent and live validation
**Date:** 2026-07-23  
**Related decision:** [ADR-0010](../adr/0010-govern-azure-mutations-per-operation.md)

## Security objective

Adding a connection or installing Vault Prospector must never change Azure. An explicitly supported
mutation may occur only for the exact reviewed target after machine policy, current Azure
authorization, local user presence, concurrency, confirmation, and audit gates all succeed.

## Protected assets

- secret values and certificate/private-key material;
- Azure identities, tokens, tenant and subscription boundaries;
- Key Vault objects, versions, policies, and availability;
- machine administrator policy;
- value-free audit evidence and diagnostic redaction;
- the user's ability to recover from a partial or conflicting operation.

## Trust boundaries

1. untrusted UI text and clipboard input into application commands;
2. standard-user process to machine-scoped administrator policy;
3. application to Windows verification;
4. application to isolated interactive/workload Azure credentials;
5. application to ARM and Key Vault data-plane endpoints;
6. provider result to encrypted local audit and metadata stores; and
7. test harness to disposable live-Azure scope.

## Threats and required controls

| Threat | Required control | Required evidence |
| --- | --- | --- |
| Broad Azure role silently enables writes | Default deny plus exact machine-policy operation/scope allowlist | Policy absence, malformed policy, broad wildcard, and ACL tests |
| Confused-deputy identity or tenant | Preview and revalidate identity, tenant, subscription, resource ID, and endpoint | Cross-tenant and swapped-identity negative tests |
| Target changed after preview | Carry an immutable preview ID and expected version/ETag into execution; regenerate on any input change | Stale-preview and concurrency-conflict tests |
| Secret leaks through preview, logs, audit, crash, or exception | Sensitive input uses bounded disposable storage; all persisted records use identifiers, hashes, lengths, and outcome only | Canary/redaction and memory-lifetime review |
| User-presence bypass | Fresh Windows verification after preview and before provider execution | All non-verified outcome tests |
| Token/session staleness | Fresh operation-specific Azure reauthentication immediately before authorization/execution | Interaction-required and revoked-token tests |
| Replay or double submission | One-time preview nonce, single-flight execution, and no automatic retry for non-idempotent calls | Double-click, retry, cancellation, and timeout tests |
| Partial provider success | Record returned version/operation ID before local refresh; show operation-specific recovery guidance | Injected post-success local failure tests |
| Destructive expansion | Separate command and policy identifier for every operation; no arbitrary REST/ARM command | Contract and UI surface review |
| Policy tampering by standard user | Machine-scoped file, trusted owner, no standard-user write ACE, strict schema, fail closed | Windows ACL and replacement-race tests |
| Malicious endpoint or vault URI | Derive endpoint from validated Azure resource ID/name and enforce expected Azure authority/suffix policy | Endpoint substitution tests |
| Audit manipulation | Encrypted append-only records with sequence and integrity chaining; never contain values | Integrity, truncation, and redaction tests |

## Operation-specific boundaries

### Secret create/new version

- A value is accepted only after the target preview is fixed.
- Updating an existing secret requires its expected current version.
- Azure creates an immutable new version; recovery guidance identifies the new version and explains
  disable/rollback options without automatically deleting it.

### Software-protected key version

- Key type, size/curve, and allowed operations are explicit allowlists.
- No private key import or export.
- The operation creates a new version and does not alter or purge earlier versions.

### Certificate policy operation

- Only an allowlisted certificate policy is supported initially.
- No PFX, PEM private key, or password input.
- Long-running operation ID is audited and safely resumable for status only.

## Explicit exclusions

This model does not authorize deletion, purge, recovery, private-key import, arbitrary access-policy
changes, RBAC assignment, managed-identity creation, service-principal creation, or generic ARM
mutation. Each requires its own threat-model extension and accepted decision.

## Enablement gates

- accepted ADR and closed internal design findings;
- production implementation with automated negative, concurrency, redaction, recovery, and UI
  tests (**complete on PR #56**);
- disposable live-Azure tests for every supported operation;
- independent security review with no open critical/high findings;
- administrator policy deployment and rollback documentation; and
- exact signed candidate validation before any public control becomes available.

## Implementation evidence

- `GovernedAzureMutationService` enforces the release switch, exact policy, fresh identity
  reauthentication, Windows verification, immutable preview, single-flight execution, typed
  confirmation, and replay denial.
- `AzureGovernedMutationProvider` implements only the four allowlisted Key Vault operations and
  checks effective data actions immediately before execution.
- `WindowsRegistryEnterprisePolicy` rejects malformed values, wildcard vault scopes, and enabled
  policy without exact operations and vault IDs.
- SQLCipher schema v7 stores value-free, sequence-numbered, hash-chained mutation audit records and
  validates chain integrity during startup.
- Application, Azure provider, platform, infrastructure, and UI tests cover default denial,
  malformed policy, conflicts, request shape, confirmation/replay, and tamper detection.
