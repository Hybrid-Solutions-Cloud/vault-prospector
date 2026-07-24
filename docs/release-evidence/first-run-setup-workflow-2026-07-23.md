# First-run setup workflow evidence

**Date:** 2026-07-23  
**Scope:** Phase 3 local production workflow and automated validation  
**Result:** Source and automated checks pass; live and exact-release gates remain open

## Implemented workflow

Vault Prospector completes Windows local verification and protected repository initialization
before exposing the main application. When no identities exist, it selects the Identities tab and
presents three ordered boundaries:

1. Windows local verification unlocked the encrypted local data for this foreground session.
2. Microsoft Entra authentication is controlled by Microsoft and retains password, passkey/FIDO,
   MFA, and Conditional Access handling.
3. Synchronization indexes metadata only; an explicit verified request is still required for a
   value.

The connection action changes with the selected method:

| Method | Action |
| --- | --- |
| Interactive user | Continue to Microsoft sign-in |
| Managed identity | Verify and connect managed identity |
| Certificate service principal | Verify and connect certificate identity |
| Federated service principal | Verify and connect federated identity |

Canceled, unavailable, unconfigured, policy-disabled, or failed Windows verification continues to
stop before repository initialization.

## Automated evidence

Focused application tests prove:

- successful local verification with an empty repository unlocks the foreground;
- first-run state is detected;
- the Identities tab is selected;
- the safe next action and status are shown; and
- all four connection methods expose an outcome-specific action.

The current app suite passes **82/82** tests. The exact synchronized repository command
also passes:

```powershell
.\scripts\Build.ps1 -Configuration Release
```

That gate completed locked restore, direct/transitive dependency-vulnerability inspection,
formatting verification, and a Release build with zero warnings and zero errors. All **254/254**
tests passed:

| Project | Passed |
| --- | ---: |
| Domain | 4 |
| Application | 53 |
| Infrastructure | 50 |
| Platform | 37 |
| Azure provider | 27 |
| App | 82 |
| Security | 1 |

## Open evidence boundary

This evidence does not substitute for:

- interactive Windows Hello success, cancellation, configuration, and policy outcomes;
- tenant consent, guest, MFA, Conditional Access, passwordless, and FIDO scenarios;
- complete keyboard, Narrator, NVDA, High Contrast, scaling, and representative-user tasks;
- independent security/accessibility review; or
- repetition against the exact installable and public candidate.

P-14 and Phase 3 therefore remain in progress.
