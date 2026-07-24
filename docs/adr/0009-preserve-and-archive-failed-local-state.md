# ADR-0009: Preserve and archive failed local state

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner and maintainers

## Context

Vault Prospector fails closed when its SQLCipher database, Windows-protected keys, offline-value
envelopes, or schema cannot be validated. Recovery must let a user start fresh when Azure remains
the system of record without silently replacing evidence, deleting a potentially recoverable key
set, or continuing with stale in-memory identity state.

The application does not support cross-device restoration of DPAPI-protected data. Copying the data
directory to another device or Windows account is not a supported backup.

## Decision

When local startup fails:

1. preserve the existing state and explain the specific recovery category;
2. do not offer reset for a newer schema—installing a compatible application is the recovery path;
3. require the exact typed phrase `RESET` plus fresh Windows verification before starting fresh;
4. move the entire local data directory to a timestamped sibling recovery archive, including the
   database and sidecars, protected keys, offline values, identity cache, settings, and logs;
5. create an empty data directory only after the archive move succeeds, and roll the move back if
   that creation fails; and
6. require application restart so no in-memory token, key, repository, or UI state crosses the
   recovery boundary.

The recovery archive remains encrypted and local. Users may delete it later after deciding recovery
or support evidence is no longer required.

## Options considered

### Delete database and key files in place

Low implementation complexity, but it can strand sidecars/caches, destroy recoverable evidence,
and leave stale process state. Rejected.

### Automatically replace failed state at startup

Simple user experience, but violates fail-closed storage and makes data-loss diagnosis impossible.
Rejected.

### Application-managed cross-device backup and restore

Potentially convenient, but securely exporting or rewrapping DPAPI-protected keys requires a
separate threat model, recovery secret design, rotation policy, and independent review. Rejected
for the current architecture.

### Archive the complete state after explicit verification

Preserves evidence, keeps related encrypted files together, supports deliberate Azure
resynchronization, and avoids automatic data loss. Accepted.

## Consequences

- Recovery needs enough free space for an atomic same-volume directory move and new empty state.
- A restart is mandatory after archival.
- Recovery archives require retention guidance and are not ordinary backups.
- Key rotation remains a separate forward-only cryptographic design and implementation task.
- Tests must cover confirmation, verification outcomes, complete archival, rollback, cancellation,
  incompatible-version behavior, and restart state.
