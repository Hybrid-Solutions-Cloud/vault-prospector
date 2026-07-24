# .NET 10 LTS migration baseline — 2026-07-24

## Scope and truth boundary

Implementation commit `03a5af014af0e26a49fca7462a02677ba825fb04` migrates the complete Windows
desktop source/test graph, locked dependency files, CI/release SDK setup, and self-contained
packaging from .NET 9 to pinned SDK `10.0.302`.

This is local compatibility and package evidence. It is not a signed public candidate,
clean-machine installed-lifecycle result, macOS iOS build, or store/release approval.

## Source changes

- All provider-neutral projects and tests target `net10.0`.
- Windows application, platform, native host, and tests target
  `net10.0-windows10.0.19041.0`.
- `global.json`, CI, and protected release automation select SDK `10.0.302`, matching the already
  pinned mobile SDK.
- All 19 desktop lock files were regenerated for their .NET 10 target graph. Direct requested and
  resolved package versions are unchanged. The lock files are smaller because .NET 10 supplies
  framework assemblies that the .NET 9 graph listed as transitive packages.
- Current build and architecture documentation names .NET 10; historical release evidence remains
  unchanged.

Microsoft’s support policy listed .NET 10 as active LTS through `2028-11-14` when this record was
created: <https://dotnet.microsoft.com/platform/support/policy/dotnet-core>.

## Locked desktop validation

Using the pinned SDK from an isolated local installation:

```powershell
$env:DOTNET_ROOT = 'D:\tmp\vault-prospector-dotnet10'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
pwsh ./scripts/Build.ps1 -Configuration Release
```

Results:

- locked restore passed;
- structured direct/transitive NuGet vulnerability inspection found no known vulnerable package;
- formatting passed;
- all projects compiled for .NET 10 with 0 warnings and 0 errors; and
- 343/343 tests passed:
  Domain 4, Application 66, Infrastructure 54, CyberArk 12, Security 1, Platform 50, Azure 27,
  BrowserProtocol 36, App 85, and BrowserHost 8.

## Mobile compatibility

The same shared .NET 10 projects completed:

- 44/44 managed mobile tests;
- Android arm64 Release build, trimming, native assembly compilation, and App Bundle production
  path with 0 warnings/errors using the existing local Android SDK/JDK; and
- Windows-hosted iOS application plus embedded credential-provider reference-pack compilation.

The first Android attempts stopped before source compilation with `XA5300` because the shell did
not expose an Android SDK and then a JDK path. Pointing `ANDROID_HOME`, `ANDROID_SDK_ROOT`, and
`JAVA_HOME` to the existing controlled local toolchains produced the passing build without a source
change. This does not replace the required hosted macOS or signed physical-device evidence.

## Windows package validation

Disposable version `0.1.0-ci.910` was built from the migration tree:

- self-contained runtime configuration reports `net10.0` and included
  `Microsoft.NETCore.App 10.0.10`;
- the self-contained application remained running five seconds after launch and used approximately
  140 MiB working set during that smoke check;
- MSI: 67,262,944 bytes,
  SHA-256 `B7F4ECC11D071625687094F5AEE6B5BD97B5C5365EEE82EA8BAC23F7CD68D3D7`;
- ZIP: 104,487,340 bytes;
- MSI major-upgrade scheduling remained rollback-safe;
- Start-menu shortcut/icon inspection passed;
- browser native-host files, three machine registrations, extension identities, and disabled
  default machine policy inspection passed;
- Chocolatey package creation passed; and
- generated WinGet manifests validated successfully.

The disposable artifacts remain ignored local test output and were not published.

## Remaining evidence

- Required exact-head GitHub jobs must execute and pass. The organization currently rejects hosted
  jobs before their first step because of a payment/spending-limit condition.
- Repeat build/package/install/upgrade/rollback/startup/uninstall on clean supported Windows using
  the exact signed candidate.
- Repeat iOS application/extension compilation on the governed macOS runner and physical devices.
- Re-run SBOM, provenance, signature, WinGet, Chocolatey, and public-asset verification on the
  immutable release candidate.
