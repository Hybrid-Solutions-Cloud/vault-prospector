# Windows security-boundary handling evidence

**Date:** 2026-07-23  
**Scope:** Phase 10 production implementation and automated verification  
**Result:** Source and automated gates pass; installed interactive lifecycle validation remains open

## Implemented boundary

`WindowsSecurityBoundaryMonitor` subscribes to the production Windows
`SystemEvents.SessionSwitch` and `SystemEvents.PowerModeChanged` events. Every session-switch reason
requires a foreground lock. Suspend and resume also require a lock; ordinary power-status changes
do not.

The application marshals a boundary notification to the Avalonia UI thread. The resulting
`LockForSystemBoundary` call:

- advances the sensitive-presentation generation;
- cancels the active operation;
- closes any in-app close prompt;
- masks the secret preview;
- clears unlocked/application-ready state; and
- requires foreground initialization and verification again.

The monitor is disposable. The application removes its callback and disposes the monitor during
desktop shutdown so the static Windows event publishers do not retain the application.

## Automated verification

The exact repository command passed:

```powershell
.\scripts\Build.ps1 -Configuration Release
```

The gate completed locked restore, direct/transitive NuGet vulnerability inspection, formatting,
and Release build with zero warnings and zero errors. All **231/231** tests passed:

| Project | Passed |
| --- | ---: |
| Domain | 4 |
| Application | 49 |
| Infrastructure | 49 |
| Platform | 37 |
| Azure provider | 25 |
| App | 66 |
| Security | 1 |

Focused tests prove that all nine Windows session-switch reasons map to a lock, suspend and resume
map to a lock, ordinary power status does not, and a system boundary immediately removes
foreground readiness and sensitive presentation.

## Evidence boundary

This is source and automated evidence, not a claim that Windows delivered each event to an
installed candidate. The connected UI-automation runtime was unavailable in this environment, and
the existing local profile contains real Vault Prospector state, so the application was not
launched against that profile.

An isolated interactive Windows account or restored VM must still exercise an installed candidate
through lock/unlock, console and remote transitions, suspend/resume, battery/AC changes, network
changes, token expiry, notification-area interaction, and assistive technology. Phase 10 therefore
remains in progress.

## Primary platform references

- [SystemEvents.SessionSwitch](https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.sessionswitch)
- [SessionSwitchReason](https://learn.microsoft.com/dotnet/api/microsoft.win32.sessionswitchreason)
- [SystemEvents.PowerModeChanged](https://learn.microsoft.com/dotnet/api/microsoft.win32.systemevents.powermodechanged)
- [PowerModes](https://learn.microsoft.com/dotnet/api/microsoft.win32.powermodes)
