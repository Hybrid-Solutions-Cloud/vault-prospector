using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
    private ReleaseUpdateInfo? _checkedReleaseUpdate;
    private VerifiedReleaseUpdate? _verifiedReleaseUpdate;

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
    private string _updateVerificationStatus =
        "No installer has been downloaded in this session.";
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
        _verifiedReleaseUpdate = null;
        UpdateLatestVersion = "Checking…";
        UpdateStatus =
            "Checking the authenticated Hybrid Solutions Cloud binary-release repository.";
        UpdateReleaseNotes =
            "Release notes will appear when trusted metadata is available.";
        UpdateVerificationStatus =
            "No installer has been downloaded in this session.";
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
                    $"Vault Prospector {release.LatestVersion} is available. Downloading requires a separate explicit action.",
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

    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    private async Task DownloadUpdateAsync()
    {
        if (releaseUpdateService is null ||
            _checkedReleaseUpdate is null)
        {
            return;
        }

        IsUpdateOperationInProgress = true;
        _verifiedReleaseUpdate = null;
        UpdateVerificationStatus =
            "Downloading to the controlled local update directory and verifying SHA-256.";
        NotifyUpdateCommandState();
        try
        {
            var update =
                await releaseUpdateService.DownloadAndVerifyAsync(
                    _checkedReleaseUpdate,
                    CancellationToken.None);
            _verifiedReleaseUpdate = update;
            UpdateVerificationStatus =
                $"Verified {update.Release.PackageName} · SHA-256 {update.Sha256}. Select Launch verified installer when ready.";
        }
        catch (OperationCanceledException)
        {
            UpdateVerificationStatus =
                "The download was cancelled. No partial installer is retained.";
        }
        catch
        {
            UpdateVerificationStatus =
                "The installer was unavailable, malformed, or failed verification. It will not be launched.";
        }
        finally
        {
            IsUpdateOperationInProgress = false;
            NotifyUpdateCommandState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunchVerifiedUpdate))]
    private async Task LaunchVerifiedUpdateAsync()
    {
        if (releaseUpdateService is null ||
            _verifiedReleaseUpdate is null)
        {
            return;
        }

        IsUpdateOperationInProgress = true;
        UpdateVerificationStatus =
            "Rechecking the installer before handing it to Windows Installer.";
        NotifyUpdateCommandState();
        try
        {
            await releaseUpdateService.LaunchAsync(
                _verifiedReleaseUpdate,
                CancellationToken.None);
            UpdateVerificationStatus =
                "Windows Installer started. Vault Prospector is locking and exiting so the user-controlled upgrade can continue.";
            LockForSystemBoundary();
            ExitRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            UpdateVerificationStatus =
                "Installer launch was cancelled. Vault Prospector remains open.";
        }
        catch
        {
            UpdateVerificationStatus =
                "The verified installer could not be launched or changed after verification. Vault Prospector remains open.";
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

    private bool CanDownloadUpdate() =>
        releaseUpdateService is not null &&
        !IsUpdateOperationInProgress &&
        _checkedReleaseUpdate?.Availability ==
        ReleaseUpdateAvailability.Available;

    private bool CanLaunchVerifiedUpdate() =>
        releaseUpdateService is not null &&
        !IsUpdateOperationInProgress &&
        _verifiedReleaseUpdate is not null;

    partial void OnIsUpdateOperationInProgressChanged(
        bool value) =>
        NotifyUpdateCommandState();

    private void NotifyUpdateCommandState()
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        LaunchVerifiedUpdateCommand.NotifyCanExecuteChanged();
    }
}
