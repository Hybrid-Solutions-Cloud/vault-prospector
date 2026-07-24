# Performance and scale

Vault Prospector treats performance as a release gate rather than an informal observation. The
automated G-05 probe exercises the production synchronization service, encrypted SQLCipher metadata
repository, and search service with synthetic metadata only.

## Supported estate profile

The baseline profile represents:

- 10 connected identities;
- 20 tenants;
- 200 subscriptions;
- 200 vaults;
- 50,000 secret, key, and certificate metadata objects; and
- 60 measured searches after query warmup.

Every identity, tenant, subscription, vault, object, tag, and fingerprint is synthetic. The probe
does not authenticate, contact Azure or CyberArk, retrieve a value, write a token, or read the
operator's Vault Prospector data directory.

## Automated targets

| Measure | Target |
| --- | --- |
| Empty encrypted repository initialization | 2 seconds or less |
| Sequential metadata synchronization of the baseline estate | 60 seconds or less |
| Encrypted repository reopen and integrity/schema validation | 5 seconds or less |
| Warm local search p95 | Less than or equal to 1 second |
| Warm local search maximum | 1.5 seconds or less |
| Cancellation response after an in-flight provider call | 500 milliseconds or less |
| Private memory after sync, warm search, and cancellation | 512 MiB or less |
| Encrypted database size | 256 MiB or less |

The search gate includes name, tag, identity, tenant, subscription, vault, object-type, enabled,
and recent-order query shapes. The one-second p95 target is the product requirement; the maximum
target detects a single severe outlier in the controlled run.

The repository derives SQLCipher v4's effective key once per repository lifetime from the existing
DPAPI-protected random key and database salt. Short-lived connections use SQLCipher's raw-key form,
so they do not repeat the 256,000-round KDF. Connection pooling remains disabled, the derived key is
held only in a mutable buffer that is zeroed on repository disposal, and a regression test proves
that existing passphrase-created databases remain readable. Metadata upserts use bounded
50-object parameterized batches, and search deterministically chooses the enabled access path with
the lowest preferred rank unless the user filters to an exact identity.

## Run

From the repository root with PowerShell 7:

```powershell
pwsh ./scripts/Test-PerformanceScale.ps1
```

The command performs locked restore and a Release build, writes a machine-readable JSON report to
`artifacts/performance-scale.json`, and exits nonzero when a target fails. Use `-NoBuild` only after
the exact source has already completed locked restore and a Release build.

## Evidence boundary

This deterministic probe establishes the encrypted-index, synchronization, local-search,
cancellation, memory, and storage baseline on its recorded environment. It deliberately excludes
provider network latency so throttling and service variability do not hide local regressions.

It does not by itself complete G-05. The exact packaged release candidate still requires:

- clean-machine process-to-usable-window startup timing;
- representative supported-device and low-resource-device repetition;
- live multi-tenant provider sync with throttling, partial failure, cancellation, and resumption;
- populated UI responsiveness and accessibility at the baseline estate size; and
- an exact-artifact report tied to the GA candidate commit and signature.
