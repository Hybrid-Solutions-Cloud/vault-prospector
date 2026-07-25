# Performance and scale baseline

**Date:** 2026-07-24

**Source commit:** `04688ff3a386a120ef9d975fe91a65a4000c1953`

**Gate status:** Automated local baseline passed; representative-device and exact packaged-candidate
evidence remains open

## Scope

The G-05 probe used the production synchronization service, SQLCipher metadata repository, and
search service with a deterministic synthetic estate:

- 10 connected identities;
- 20 tenants;
- 200 subscriptions;
- 200 vaults;
- 50,000 secret, key, and certificate metadata objects; and
- 60 timed searches after warmup.

The generated estate contains no provider values, credentials, tokens, tenant data, subscription
data, or other live identifiers. Provider network time is excluded so the result isolates local
application and encrypted-storage behavior.

The empty-repository metric performs one isolated, unmeasured initialization first so that
first-use .NET JIT, SQLCipher native loading, and cryptographic-provider activation are not
mistaken for repository work. Clean process-to-usable-window startup remains a separate
exact-package live gate and is not claimed by this metric.

## Environment

| Property | Observed value |
| --- | --- |
| Operating system | Windows NT 10.0.26100 |
| Runtime | .NET 9.0.18 |
| Process architecture | x64 |
| Logical processors | 8 |
| Configuration | Release |

## Exact-source results

Command:

```powershell
pwsh ./scripts/Test-PerformanceScale.ps1 `
  -OutputPath ./TestResults/performance-scale-04688ff.json `
  -NoBuild
```

| Measure | Result | Limit | Status |
| --- | ---: | ---: | --- |
| Empty encrypted repository initialization | 328.974 ms | 2,000 ms | Pass |
| Synchronize 50,000 metadata objects | 6,594.774 ms | 60,000 ms | Pass |
| Encrypted repository reopen and validation | 1,318.319 ms | 5,000 ms | Pass |
| Warm local search p95 | 262.345 ms | 1,000 ms | Pass |
| Warm local search maximum | 274.941 ms | 1,500 ms | Pass |
| In-flight synchronization cancellation response | 3.921 ms | 500 ms | Pass |
| Private memory after sync/search/cancellation | 41.438 MiB | 512 MiB | Pass |
| Encrypted database size | 24.594 MiB | 256 MiB | Pass |

The machine-readable report returned `passed: true` and named the exact source commit.

## Defects exposed and remediated

The first controlled run found real performance failures:

- individual item commands made the 50,000-object metadata synchronization exceed three minutes;
  and
- the grouped multi-access search query produced a 3,532 ms p95 and 6,047 ms maximum.

The remediation:

- performs bounded 50-object parameterized upsert batches inside the existing transaction;
- derives SQLCipher v4's effective raw key once per repository lifetime, preserving the existing
  passphrase-created on-disk format while avoiding a repeated 256,000-round KDF;
- keeps connection pooling disabled so recovery, rotation, hashing, and cleanup do not inherit
  persistent database file handles;
- zeroes the mutable derived-key buffer when the repository is disposed; and
- replaces nondeterministic grouping with an explicit enabled access-path rank, while preserving
  exact-identity filtering and every search filter.

The infrastructure suite passes 56/56 tests, including legacy passphrase-database compatibility,
wrong/missing key behavior, key rotation and crash recovery, deterministic preferred access, and
identity filtering. The complete locked Release gate passes vulnerability inspection, formatting,
a zero-warning/error build, and 345/345 tests.

## Remaining G-05 evidence

This baseline does not claim the full release gate. Still required against the exact signed release
candidate:

- clean-machine process-to-usable-window startup;
- representative supported and low-resource Windows devices;
- live provider synchronization with throttling, partial failures, cancellation, and resumption;
- populated UI responsiveness and accessibility at the supported estate size; and
- repetition tied to the immutable candidate commit, hashes, signature, and distribution paths.
