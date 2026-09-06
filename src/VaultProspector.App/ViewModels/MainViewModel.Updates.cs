using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
    private ReleaseUpdateInfo? _checkedReleaseUpdate;

    [ObservableProperty]
    private string _updateCurrentVersion = GetVersion();
    [ObservableProperty]
    private string _updateLatestVersion = "Not checked";
    [ObservableProperty]
    private string _updateStatus =
        "Select Check for updates to query the authenticated public binary-release repository.";
    [ObservableProperty]
    private string _updateReleaseNotes =
        "Release notes appear here after a successful check.";
    [ObservableProperty]
    private bool _isUpdateOperationInProgress;

    public bool IsUpdateServiceAvailable =>
        releaseUpdateService is not null;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        if (releaseUpdateService is null)
            return;

        IsUpdateOperationInProgress = true;
        _checkedReleaseUpdate = null;
        UpdateLatestVersion = "Checking…";
        UpdateStatus =
            "Checking the authenticated Hybrid Solutions Cloud binary-release repository.";
        UpdateReleaseNotes =
            "Release notes will appear when trusted metadata is available.";
        NotifyUpdateCommandState();
        try
        {
            var release = await releaseUpdateService.CheckAsync(
                CancellationToken.None);
            _checkedReleaseUpdate = release;
            UpdateCurrentVersion = release.CurrentVersion;
            UpdateLatestVersion = release.LatestVersion;
            UpdateReleaseNotes = release.ReleaseNotes;
            UpdateStatus = release.Availability switch
            {
                ReleaseUpdateAvailability.Available =>
                    $"Vault Prospector {release.LatestVersion} is available. Select Install and verify update to download, verify, and start the Windows upgrade.",
                ReleaseUpdateAvailability.Current =>
                    $"Vault Prospector {release.CurrentVersion} is current for this release channel.",
                _ =>
                    $"This development build cannot be upgraded in place. The latest supported package is {release.LatestVersion}.",
            };
        }
        catch (OperationCanceledException)
        {
            UpdateLatestVersion = "Not checked";
            UpdateStatus =
                "The update check was cancelled. Normal application use is unaffected.";
        }
        catch
        {
            UpdateLatestVersion = "Unavailable";
            UpdateStatus =
                "Trusted update information is unavailable or malformed. Normal application use is unaffected; try again later.";
            UpdateReleaseNotes =
                "No unverified release information was displayed.";
        }
        finally
        {
            IsUpdateOperationInProgress = false;
            NotifyUpdateCommandState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallReleaseUpdate))]
    private async Task InstallReleaseUpdateAsync()
    {
        if (releaseUpdateService is null)
            return;

        IsUpdateOperationInProgress = true;
        NotifyUpdateCommandState();
        try
        {
            UpdateStatus = _checkedReleaseUpdate is null
                ? "Checking for the newest trusted release before installation."
                : $"Preparing Vault Prospector {_checkedReleaseUpdate.LatestVersion} for installation.";
            var release = _checkedReleaseUpdate ??
                await releaseUpdateService.CheckAsync(CancellationToken.None);
            _checkedReleaseUpdate = release;
            UpdateCurrentVersion = release.CurrentVersion;
            UpdateLatestVersion = release.LatestVersion;
            UpdateReleaseNotes = release.ReleaseNotes;
            if (release.Availability != ReleaseUpdateAvailability.Available)
            {
                UpdateStatus = release.Availability ==
                    ReleaseUpdateAvailability.Current
                    ? $"Vault Prospector {release.CurrentVersion} is already current."
                    : $"This development build cannot be upgraded in place. The latest supported package is {release.LatestVersion}.";
                return;
            }

            UpdateStatus =
                $"Downloading Vault Prospector {release.LatestVersion} to the protected update directory and verifying its SHA-256 identity.";
            var verified = await releaseUpdateService.DownloadAndVerifyAsync(
                release,
                CancellationToken.None);
            UpdateStatus =
                $"Verified {verified.Release.PackageName}. Rechecking it before requesting Windows administrator approval.";
            await releaseUpdateService.LaunchAsync(
                verified,
                CancellationToken.None);
            UpdateStatus =
                "Windows Installer started. Vault Prospector is locking and exiting so the upgrade can continue.";
            LockForSystemBoundary();
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            UpdateStatus =
                "The update was cancelled. No partial installer is retained and Vault Prospector remains open.";
        }
        catch
        {
            UpdateStatus =
                "The update could not be downloaded, verified, or started. No unverified installer was launched; Vault Prospector remains open.";
        }
        finally
        {
            IsUpdateOperationInProgress = false;
            NotifyUpdateCommandState();
        }
    }

    private bool CanCheckForUpdates() =>
        releaseUpdateService is not null &&
        !IsUpdateOperationInProgress;

    private bool CanInstallReleaseUpdate() =>
        releaseUpdateService is not null &&
        !IsUpdateOperationInProgress;

    partial void OnIsUpdateOperationInProgressChanged(
        bool value) =>
        NotifyUpdateCommandState();

    private void NotifyUpdateCommandState()
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        InstallReleaseUpdateCommand.NotifyCanExecuteChanged();
    }
}
