using System.Text.Json;
using VaultProspector.App;
using VaultProspector.App.ViewModels;
using VaultProspector.Domain;

namespace VaultProspector.App.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-app-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task MissingSettingsUseProductRegistrationWithoutCustomMode()
    {
        var store = new AppSettingsStore(Path.Combine(_directory, "settings.json"));

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProductIdentity.DefaultClientId, settings.ClientId);
        Assert.False(settings.UseCustomClientId);
    }

    [Fact]
    public async Task LegacyEmptySettingsMigrateToProductRegistration()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { ClientId = "", ClipboardClearSeconds = 30, OfflineCacheEnabled = false, MaximumCacheHours = 8 }), TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProductIdentity.DefaultClientId, settings.ClientId);
        Assert.False(settings.UseCustomClientId);
    }

    [Fact]
    public async Task LegacyCustomClientIdIsPreservedAndInferred()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        const string customClientId = "11111111-1111-1111-1111-111111111111";
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { ClientId = customClientId, ClipboardClearSeconds = 30, OfflineCacheEnabled = false, MaximumCacheHours = 8 }), TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(customClientId, settings.ClientId);
        Assert.True(settings.UseCustomClientId);
    }

    [Fact]
    public async Task ExplicitCustomModeWithNullClientIdFailsLaterAsActionableValidation()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{\"ClientId\":null,\"UseCustomClientId\":true}", TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, settings.ClientId);
        Assert.True(settings.UseCustomClientId);
    }

    [Fact]
    public void ErrorMappingProvidesRecoveryWithoutEchoingSensitiveMessage()
    {
        const string sensitiveMessage = "secret-value-that-must-not-appear";

        var error = UserFacingErrorMapper.From(new InvalidOperationException(sensitiveMessage));

        Assert.Equal("The action is blocked by current policy", error.Title);
        Assert.DoesNotContain(sensitiveMessage, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, error.Recovery, StringComparison.Ordinal);
        Assert.NotEmpty(error.Recovery);
    }

    [Fact]
    public void WindowsVerificationFailureExplainsThatNoValueWasReleased()
    {
        var error = UserFacingErrorMapper.From(new UnauthorizedAccessException("internal"));

        Assert.Contains("not reveal", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Hello", error.Recovery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptedSettingsFailureProvidesNarrowRecoveryWithoutDeletingProtectedData()
    {
        var error = UserFacingErrorMapper.From(new JsonException("internal file content"));

        Assert.Contains("settings.json", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not deleted", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal file content", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("internal file content", error.Recovery, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityActionsTrackSelectionAndBusyState()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));

        viewModel.SelectedIdentity = new ConnectedIdentity(
            Guid.NewGuid(),
            ProductIdentity.DefaultClientId,
            "account",
            "user@example.invalid",
            "Test identity",
            "tenant",
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);

        Assert.True(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.True(viewModel.RemoveIdentityCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));

        viewModel.IsBusy = false;
        viewModel.SelectedIdentity = null;

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));
    }

    [Fact]
    public void ResultActionsRequireCompatibleSelectionAndCachePolicy()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Key);

        Assert.True(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.CopyCommand.CanExecute(null));
        Assert.False(viewModel.OpenOfflineCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedCachedValueCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Secret);

        Assert.True(viewModel.RevealCommand.CanExecute(null));
        Assert.True(viewModel.CopyCommand.CanExecute(null));
        Assert.True(viewModel.OpenOfflineCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedCachedValueCommand.CanExecute(null));
        Assert.False(viewModel.CacheSelectedCommand.CanExecute(null));

        viewModel.OfflineCacheEnabled = true;

        Assert.True(viewModel.CacheSelectedCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.CacheSelectedCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));
    }

    [Fact]
    public void WorkspaceActionsRequireTheirExactPrerequisites()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.WorkspaceName = "  ";
        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        viewModel.WorkspaceName = "Operations";
        Assert.True(viewModel.CreateWorkspaceCommand.CanExecute(null));

        viewModel.SelectedWorkspace = new Workspace(Guid.NewGuid(), "Operations", string.Empty, 0);

        Assert.True(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Certificate);
        Assert.True(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));

        viewModel.SelectedIdentity = CreateIdentity();
        Assert.True(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.IsBusy = false;
        viewModel.SelectedWorkspace = null;

        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));
    }

    [Fact]
    public void GeneralOperationsDisableWhileBusyAndEmptySelectionFiltersReset()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.AddIdentityCommand.CanExecute(null));
        Assert.True(viewModel.SearchCommand.CanExecute(null));
        Assert.True(viewModel.SaveSettingsCommand.CanExecute(null));
        Assert.True(viewModel.PurgeAllCachedValuesCommand.CanExecute(null));

        viewModel.SelectedIdentity = CreateIdentity();
        viewModel.SelectedWorkspace = new Workspace(Guid.NewGuid(), "Operations", string.Empty, 0);
        Assert.True(viewModel.HasSelectedIdentity);
        Assert.True(viewModel.HasSelectedWorkspace);
        viewModel.FilterSelectedIdentity = true;
        viewModel.FilterSelectedWorkspace = true;

        viewModel.SelectedIdentity = null;
        viewModel.SelectedWorkspace = null;

        Assert.False(viewModel.HasSelectedIdentity);
        Assert.False(viewModel.HasSelectedWorkspace);
        Assert.False(viewModel.FilterSelectedIdentity);
        Assert.False(viewModel.FilterSelectedWorkspace);

        viewModel.IsBusy = true;

        Assert.False(viewModel.AddIdentityCommand.CanExecute(null));
        Assert.False(viewModel.SearchCommand.CanExecute(null));
        Assert.False(viewModel.SaveSettingsCommand.CanExecute(null));
        Assert.False(viewModel.PurgeAllCachedValuesCommand.CanExecute(null));
    }

    private static MainViewModel CreateViewModel() => new(null!, null!, null!, null!, null!, null!, null!, null!);

    private static ConnectedIdentity CreateIdentity() => new(
        Guid.NewGuid(),
        ProductIdentity.DefaultClientId,
        "account",
        "user@example.invalid",
        "Test identity",
        "tenant",
        AuthenticationState.Ready,
        DateTimeOffset.UtcNow);

    private static SearchResultRow CreateResult(VaultObjectType objectType)
    {
        var vaultId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var vault = new VaultResource(
            vaultId,
            $"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/{vaultId:N}",
            "vault",
            "tenant",
            "subscription",
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://vault.vault.azure.net/"),
            now);
        var item = new VaultItem(
            Guid.NewGuid(),
            vaultId,
            "item",
            objectType,
            true,
            new Dictionary<string, string>(),
            null,
            now,
            now,
            null,
            "version",
            "fingerprint",
            now);
        return new SearchResultRow(new SearchResult(item, vault, "identity", "tenant", false, null, false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }
}
