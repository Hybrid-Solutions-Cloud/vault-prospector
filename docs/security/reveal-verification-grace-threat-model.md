# Reveal-verification grace threat model

## Decision and boundary

Vault Prospector may reuse a recent successful Windows verification for subsequent **explicit
Reveal** actions for Off, 30, 60, or 120 seconds. Off is the default. Machine policy can shorten the
user choice or force Off with `MaximumRevealVerificationGraceSeconds`.

The grace period is an authorization timestamp, not a secret cache. Each Reveal still resolves the
currently selected metadata, retrieves only that value from its provider, displays it for at most
10 seconds, and disposes the sensitive buffer. Nothing is prefetched or persisted.

Copy, encrypted offline cache/open, recovery, administrative actions, browser fill, and CyberArk
operations continue to perform their independent verification. They cannot consume this session.

## Threats and controls

| Threat | Control | Evidence |
| --- | --- | --- |
| Wall-clock rollback extends authorization | Expiration uses `TimeProvider.GetTimestamp()` monotonic elapsed time, not UTC wall time. | Deterministic clock tests |
| A background or unattended window retains authorization | Manual lock, minimize/notification-area transition, Windows session transition, suspend, and resume synchronously invalidate the session and hide presented values. | View-model and Windows-boundary tests |
| Authorization crosses an identity or workspace boundary | Either selection change invalidates before the next Reveal. | View-model invalidation tests |
| A relaxed user choice overrides enterprise policy | Effective duration is the minimum of the user choice, product maximum, and machine cap; invalid policy forces Off. | Policy parser and session tests |
| Policy changes are noticed too late | Every eligible Reveal rereads policy and compares a complete non-secret boundary stamp before reuse. A changed stamp discards the session. | Policy-change tests |
| Concurrent Reveal commands race into multiple prompts or extend a session | A single asynchronous verification lock serializes authorization. Only the successful prompt timestamp starts the bounded session. | Concurrency tests |
| A failed or canceled prompt leaves earlier authorization usable | Starting a new verification clears prior state; every non-verified outcome invalidates. | Failure/cancellation tests |
| Grace leaks into higher-risk operations | Only the explicit `RetrieveAsync` overload accepts the reveal session. Existing copy/cache/offline/browser/recovery paths continue to call the verifier directly. | Application-service tests |

## Invalidation events

The session ends immediately on:

- expiration;
- manual lock;
- minimize or notification-area transition;
- any Windows session switch;
- suspend or resume;
- connected-identity selection change;
- workspace selection change;
- user grace-setting change;
- any machine-policy boundary change; or
- canceled, unavailable, denied, or failed verification.

Restarting the application never restores a grace session.

## Required live validation

Before closing AB#5608, use the exact packaged candidate to reveal at least three distinct secrets
with Off, 30, 60, and 120 seconds; confirm the expected prompt count and 10-second value masking.
Repeat while triggering every invalidation event above. Record only timestamps, selected setting,
policy cap, prompt count, and pass/fail outcome—never secret values or names.
