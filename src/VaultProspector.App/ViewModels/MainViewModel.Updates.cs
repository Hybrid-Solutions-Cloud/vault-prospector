using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultProspector.Application;

namespace VaultProspector.App.ViewModels;

public sealed partial class MainViewModel
{
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
            UpdateCurrentVersion = release.CurrentVersion;
            UpdateLatestVersion = release.LatestVersion;
            UpdateReleaseNotes = release.ReleaseNotes;
            UpdateStatus = release.Availability switch
            {
                ReleaseUpdateAvailability.Available =>
                    $"Vault Prospector {release.LatestVersion} is available. Open Release history to download it, then follow Install & verify releases before running the MSI.",
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

    private bool CanCheckForUpdates() =>
        releaseUpdateService is not null &&
        !IsUpdateOperationInProgress;

    partial void OnIsUpdateOperationInProgressChanged(
        bool value) =>
        NotifyUpdateCommandState();

    private void NotifyUpdateCommandState()
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
    }
}
