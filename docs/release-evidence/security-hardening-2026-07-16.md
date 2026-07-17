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
| Identity persistence rollback | A successful interactive MSAL sign-in followed by encrypted metadata persistence failure could leave an account in the app-owned token cache with no UI identity through which to remove it. | Identity addition now compensates with token-cache removal using a non-cancelled cleanup token. A regression test injects metadata failure and proves the authenticated identity is removed. If both persistence and rollback fail, both exceptions are surfaced for remediation. |
| Offline access audit | Opening an encrypted offline value did not record the non-sensitive access event, and adding that audit boundary without exceptional cleanup could release a decrypted value when metadata was unavailable. | Offline opens now write access history before returning. An injected audit failure proves the retrieved `SensitiveValue` is disposed and never returned. |
| Clipboard plaintext lifetime | Clipboard ownership comparison retained a second immutable plaintext string until timeout or exit, and a non-positive interval could fault only inside the detached clear task. | Ownership now retains only a SHA-256 digest, zeroizes replaced/current digest buffers, compares in fixed time, and rejects non-positive intervals before writing to the system clipboard. Regression tests protect the no-string ownership state and pre-copy validation. |

## Verification

- The 2026-07-17 locked Release gate passed structured direct/transitive vulnerability inspection,
  formatting verification, a 0-warning/0-error build, and 88/88 tests across all seven projects.
- The test build passed warnings-as-errors analyzers after synchronization ownership was changed
  from a disposable semaphore to a serialized asynchronous operation queue.
- The hardened paths add no logging of secret values, tokens, DPAPI keys, or decrypted cache payloads; cleanup failure uses the existing redacting diagnostic sink.

## Remaining required evidence

- An independent reviewer must assess the implementation and threat model and track all critical/high findings to closure.
- The [independent security review plan](../security/independent-review-plan.md) must be executed by
  someone who did not implement the assessed candidate; this internal hardening pass cannot approve
  P-08.
- Windows Hello must be exercised with success, cancellation, unavailable hardware, PIN/biometric failure, session lock, and policy-controlled scenarios.
- Runtime clipboard tests must cover the configured timeout, replacement content, application close, clipboard contention, history, remote-session behavior, and OS shutdown limitations.
- SQLCipher key lifetime in managed connection configuration and immutable strings returned by platform SDKs remain explicit managed-runtime residual risks for independent review.
