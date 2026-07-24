# ADR-0011: Local key rotation and device replacement

**Status:** Accepted  
**Date:** 2026-07-23  
**Deciders:** Vault Prospector product owner and maintainers

## Context

Vault Prospector protects the SQLCipher database with a random key wrapped for the current Windows
user and protects opt-in offline values with versioned AES-GCM keys, also wrapped for that user.
Database, wrapped key, cache envelopes, token cache, and settings form one local state set.

Rotating only a wrapped key can permanently strand its ciphertext. Copying DPAPI-bound state to a
different device/profile does not make it recoverable. Rotation, backup, reinstall, device
replacement, and unrecoverable-key behavior therefore need explicit and different semantics.

## Decision

### Metadata-database key rotation

Database rotation is implemented as an internal maintenance engine. It is not exposed as a user
command until independent review approves it:

1. require an idle process, typed confirmation, free-space preflight, and fresh Windows
   verification;
2. archive the complete matched local state using ADR-0009 before mutation;
3. generate and stage a new random key protected for the current Windows user;
4. write an HMAC-authenticated rotation journal using a separate DPAPI-protected journal key; the
   journal contains no plaintext encryption key;
5. use SQLCipher rekey in one exclusive database connection, re-encrypt every valid offline
   envelope under its staged replacement key, and run database, relationship, and envelope
   validation;
6. atomically publish each staged wrapped key, remove previous keys only after validation, and
   delete the journal last; and
7. on any interruption after journal publication, validate the journal and archive manifest, move
   the interrupted state to a support archive, and atomically restore the matched pre-rotation
   state before repository initialization.

Failure-injection tests cover archive and journal publication, key staging, SQLCipher rekey,
offline re-encryption, both key publications, replacement validation, old-key cleanup, and a crash
between the two atomic moves inside key publication. Tampered journals or archives fail closed
without replacing active state.

### Offline-value key rotation

The current envelope format authenticates its format/key version and complete descriptor. During a
rotation, every envelope is first authenticated with the current key, re-encrypted through atomic
file replacement under the staged key, and validated again after key publication. The matched
pre-rotation archive is the rollback boundary, so a partial envelope set is never accepted after an
interruption. The old wrapped key is retained until every envelope and the metadata database pass
post-rotation validation.

Future multi-generation lazy rotation would require a separate format decision. The implemented
engine deliberately performs an all-or-rollback rotation instead.

### Backup and reinstall

- Uninstall/reinstall on the same Windows account is supported because uninstall retains the local
  state directory.
- A manual same-account recovery copy is useful only when the complete data directory, including
  all wrapped keys, is captured and restored as one quiescent matched set.
- Vault Prospector does not currently create portable backups, recovery passwords, escrow keys, or
  cloud synchronization.

### Device/profile replacement

Cross-device or cross-profile restoration of the DPAPI-bound state is unsupported. The supported
replacement workflow is to install Vault Prospector, reconnect Azure identities, resynchronize
metadata, and explicitly cache any offline values again. Azure remains authoritative.

### Unrecoverable keys

Missing/unusable keys never trigger replacement in place. The application preserves the encrypted
state, offers support/recovery guidance, and—after exact `RESET` confirmation plus fresh Windows
verification—archives it and starts fresh after restart.

### Recovery archive retention

Recovery archives are retained indefinitely by default because an automatic age or size policy
could delete the only matched-key recovery set or incident evidence. The Settings page inventories
only app-generated reset, pre-rotation, and failed-rotation archive directories. It reports their
type, creation time, and size without opening protected values.

Permanent deletion is an explicit per-archive action that requires selecting the archive, typing
`DELETE ARCHIVE` exactly, and completing fresh Windows verification. The filesystem adapter accepts
only canonical app-generated direct-child names, refuses reparse points and path traversal, and
refuses deletion while a rotation-recovery journal is active. It atomically moves the selected
directory to a same-volume quarantine name, validates the tree again, and deletes only that exact
directory. Failed deletion restores the original archive name when possible.

## Options considered

### Silently generate a replacement key

Rejected because it destroys the relationship between ciphertext and key and masks data loss.

### Export a password-protected portable backup

Rejected for the current architecture. Password KDF parameters, recovery-secret handling, escrow,
rotation, compromise response, and cross-device trust require a separate threat model and
independent review.

### Rely on ordinary profile/cloud backup

Rejected as a supported promise. Backup products may not capture a quiescent matched set and DPAPI
restoration semantics vary across account/domain/device recovery.

### Journaled all-or-rollback rotation with a complete pre-rotation archive

Accepted as the required future implementation. It gives every crash boundary a deterministic
recovery path without plaintext fallback.

## Consequences

- The rotation engine and crash recovery are implemented and tested internally, but user exposure
  and GA remain gated on independent review and live Windows failure testing.
- Cross-device migration intentionally resynchronizes from Azure and does not carry offline values.
- Recovery archives can consume significant disk space; users can inspect and explicitly delete
  individual archives, but there is intentionally no automatic retention policy.
- Independent review must cover SQLCipher rekey, DPAPI generation publication, envelope migration,
  journal integrity, crash consistency, archive deletion containment, memory clearing, and file
  ACLs.
