# Internal security hardening — 2026-07-16

## Scope and independence

Codex performed a focused implementation review of the protected-key, encrypted offline-value,
secret-access, sensitive-memory, and clipboard paths. This is implementation hardening evidence,
not the independent security review required by P-08. P-04 and P-05 remain in progress until their
independent and Windows-runtime gates pass.

## Findings remediated

| Boundary | Finding | Remediation and regression evidence |
| --- | --- | --- |
| DPAPI key purpose | Removing unsupported characters could map distinct purposes to one filename while deriving different DPAPI entropy, causing collision and recovery failure; Windows case folding and reserved device names created additional aliases. | Purposes must now be 1–64 lowercase ASCII letters, digits, hyphens, or underscores, start alphanumerically, and not be a reserved Windows filename. Seven invalid/colliding forms are rejected before directory creation. |
| Encrypted offline value | Replacing an existing cache file wrote directly to its published path, so cancellation or process loss could truncate the prior valid envelope. | AES-GCM envelopes are written to a unique same-directory temporary file and atomically moved over the published path; replacement tests prove one complete current envelope and no temporary residue. |
| Retrieved secret lifetime | If access-history persistence failed after Azure returned a value, the exceptional path did not deterministically dispose that value. | The exceptional path now disposes the returned `SensitiveValue`; a regression test injects metadata failure and proves disposal. `SensitiveValue` also performs best-effort finalizer zeroization if a caller violates its disposal contract. |
| Cached object type | Cached retrieval repeated identity/fingerprint and user-verification checks but did not independently repeat the secret-only object-type boundary. | Non-secret metadata is rejected before Windows verification or protected-value access; regression coverage proves both downstream services remain untouched. |
| Clipboard ownership | Independent delayed clears could race with a later copy, and orderly application exit did not attempt to clear a still-owned value. | Clipboard mutations use serialized generation leases. An old timer cannot clear a replacement lease, unrelated clipboard content is preserved, and orderly window close clears only the unchanged value owned by Vault Prospector. Cleanup failure is recorded through the redacting diagnostic sink and does not crash shutdown. |

## Verification

- Release test execution after these changes: 57 passed, 0 failed, 0 skipped.
- The test build passed warnings-as-errors analyzers after synchronization ownership was changed from a disposable semaphore to a serialized asynchronous operation queue.
- The hardened paths add no logging of secret values, tokens, DPAPI keys, or decrypted cache payloads; cleanup failure uses the existing redacting diagnostic sink.

## Remaining required evidence

- An independent reviewer must assess the implementation and threat model and track all critical/high findings to closure.
- Windows Hello must be exercised with success, cancellation, unavailable hardware, PIN/biometric failure, session lock, and policy-controlled scenarios.
- Runtime clipboard tests must cover the configured timeout, replacement content, application close, clipboard contention, history, remote-session behavior, and OS shutdown limitations.
- SQLCipher key lifetime in managed connection configuration and immutable strings returned by platform SDKs remain explicit managed-runtime residual risks for independent review.
