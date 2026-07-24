# Local encryption rotation evidence — 2026-07-23

## Scope

This evidence covers the internal, unreleased all-or-rollback local encryption rotation engine. It
does not approve a user-facing rotation command or satisfy the independent-review/live-power-loss
release gates.

## Implemented controls

- SQLCipher WAL checkpoint and integrity/relationship validation before archive.
- Free-space preflight and complete local-state copy to a sibling recovery directory.
- Per-file SHA-256 verification plus HMAC-authenticated archive manifest.
- HMAC-authenticated rotation journal under a separate DPAPI-protected key.
- Staged DPAPI-protected replacements for metadata and offline-value keys.
- SQLCipher rekey followed by reopen under only the replacement key.
- Authenticate-before-re-encrypt and atomic replacement for every offline envelope.
- Post-publication database and offline-envelope validation.
- Old key removal only after validation; journal deletion last.
- Startup rollback to the verified matched archive after any journaled interruption; interrupted
  state is retained separately for support.
- Path containment, reparse-point rejection, and fail-closed behavior for tampered/missing journal
  or archive material.
- Settings inventory for canonical reset, pre-rotation, and failed-rotation archives, including
  creation time and byte size without decrypting protected content.
- Permanent per-archive deletion only after selection, exact `DELETE ARCHIVE` confirmation, fresh
  Windows verification, canonical direct-child validation, reparse-point rejection, and an
  active-rotation-journal check. Automatic retention is intentionally disabled.

## Automated evidence

`LocalEncryptionRotationTests` covers:

- successful metadata and offline-value rotation, replacement-key readability, and old-key
  rejection;
- injected failure after archive, journal, key staging, database rekey, offline re-encryption,
  metadata-key publication, offline-key publication, replacement validation, and old-key cleanup;
- failure between moving the current key aside and publishing the staged key;
- journal tampering and archive tampering without active-state replacement.

`WindowsDataProtectionKeyProviderTests` covers staged publication, completion, abort, concurrent
initial publication, purpose isolation, missing-key failure, and purpose/path validation.

`FileSystemLocalRecoveryArchiveStoreTests`, `LocalRecoveryArchiveServiceTests`, and the app command
test cover generated-name/type/time/size inventory, unknown-name exclusion, exact-target deletion,
path-traversal rejection, active-journal refusal, confirmation-before-verification ordering, every
non-verified result, redacted audit emission, selection/command state, and post-delete refresh.
The focused archive suites passed 14/14, including fail-closed behavior when the pre-delete audit
record cannot be written.

One captured rotation run exposed a transient Windows `UnauthorizedAccessException` while replacing
the authenticated journal: 12/13 cases passed and the failing case stopped before its injected
checkpoint. Journal publication now retries only `IOException`/`UnauthorizedAccessException`
failures seven times with bounded cancellation-aware backoff; a persistent ACL or filesystem
failure still propagates and leaves recovery material intact. A fresh filtered run then passed
13/13 in 1 minute 12 seconds. The Release solution build completed with zero warnings and zero
errors. The exact post-remediation locked gate passed restore, direct/transitive vulnerability
inspection, formatting, Release build, and all 210/210 tests across seven projects with coverage
artifacts for every project.

## Open release gates

- Independent cryptographic, DPAPI, SQLCipher, filesystem, ACL, and memory-lifetime review.
- Live forced termination and power-loss tests on supported Windows 10/11 filesystems.
- User-presence/typed-confirmation UX for exposing the rotation command.
- Clean-machine uninstall/reinstall validation and support runbook exercise.
- Trusted signed release evidence.
