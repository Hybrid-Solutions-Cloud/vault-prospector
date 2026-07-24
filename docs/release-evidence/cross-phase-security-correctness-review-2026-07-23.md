# Cross-phase security and correctness review evidence

**Date:** 2026-07-23  
**Scope:** Accumulated local Phase 3–10 implementation  
**Result:** Internal review findings remediated; exact local Release gate passed

## Findings closed

- Identity revocation now commits the revoked state before best-effort provider cleanup, purges
  every associated historical vault scope even when provider cleanup fails, attempts all scopes,
  and reports any residual purge failure without restoring access.
- A dedicated identity-scoped purge action is available without requiring identity removal or
  revocation. It includes removed access paths and deduplicates vault scopes.
- Authentication exceptions can no longer be persisted in synchronization error records; a fixed,
  non-sensitive interaction-required message is stored instead.
- Microsoft Graph and ARM JSON responses are limited to 8 MiB before parsing. Graph pagination
  accepts only HTTPS, default-port `graph.microsoft.com` locations.
- Protected-value envelopes and authenticated rotation documents are limited to 16 MiB, and the
  settings file is limited to 64 KiB before JSON parsing.
- Tray text preserves both the locked boundary and operational context.
- Rotation recovery retries transient Windows directory-swap I/O/access failures with finite,
  cancellation-aware backoff. After active state moves, archive promotion or rollback completes
  without later cancellation so the canonical data path is not stranded.

The review found no Azure mutation implementation. Diagnostic output remains field-allowlisted and
identifier-pseudonymized. No known vulnerable direct or transitive NuGet packages were reported.
This was an internal engineering review and does not approve the independent-review gate.

## Automated evidence

The authoritative command was:

```powershell
.\scripts\Build.ps1 -Configuration Release
```

The final rerun completed:

- locked restore;
- direct and transitive NuGet vulnerability inspection;
- formatting verification;
- Release build with zero warnings and zero errors;
- **254/254** tests across all seven test projects; and
- Cobertura coverage artifacts for all seven projects.

| Project | Passed |
| --- | ---: |
| Domain | 4 |
| Application | 53 |
| Infrastructure | 50 |
| Platform | 37 |
| Azure provider | 27 |
| App | 82 |
| Security | 1 |

The first full run exposed one transient Windows access denial at the
`OfflineKeyPublished` injected crash boundary. After bounded directory-swap retry was added, all
nine injected crash checkpoints passed, followed by the complete 254-test gate above.

## Remaining boundary

Live Azure tenant/permission matrices, installed Windows lifecycle testing, representative-user
and assistive-technology validation, exact packaged-candidate repetition, and independent security
review remain required by the readiness matrix.
