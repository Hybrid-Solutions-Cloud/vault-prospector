# Repository commands

Run from the repository root with PowerShell 7.

```powershell
pwsh ./scripts/Build.ps1 -Configuration Release
pwsh ./scripts/PackageInstaller.ps1 -Version 0.1.0-ci.1
pwsh ./scripts/Test-InstallerUpgradeSchedule.ps1 -InstallerPath ./artifacts/VaultProspector-0.1.0-ci.1-win-x64.msi
pwsh ./scripts/Test-InstallerShortcutIcon.ps1 -InstallerPath ./artifacts/VaultProspector-0.1.0-ci.1-win-x64.msi
pwsh ./scripts/Test-BrowserHostInstaller.ps1 -InstallerPath ./artifacts/VaultProspector-0.1.0-ci.1-win-x64.msi -PublishDirectory ./artifacts/publish-win-x64
```

Browser extension:

```powershell
Set-Location browser-extension
npm test
npm run build
```

Use locked restore through `Build.ps1`. Package artifacts and test-result directories are ignored
and must not be committed.

