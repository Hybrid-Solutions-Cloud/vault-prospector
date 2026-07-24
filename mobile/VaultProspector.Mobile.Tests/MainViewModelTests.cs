using Azure.Core;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Mobile.Core;
using VaultProspector.Mobile.ViewModels;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task UnlockInitializesEncryptedUseCasesAndLoadsIdentities()
    {
        var platform = new Platform();
        using var useCases = new UseCases();
        using var viewModel = new MainViewModel(platform, useCases);

        await viewModel.InitializeAsync();
        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLocked);
        Assert.True(useCases.Initialized);
        Assert.Single(viewModel.Identities);
        Assert.Equal(useCases.Identity, viewModel.SelectedIdentity);
        Assert.Equal([false, false], platform.CoverStates);
    }

    [Fact]
    public async Task BackgroundLockClearsSearchAndRevealedValue()
    {
        var platform = new Platform();
        using var useCases = new UseCases();
        using var viewModel = new MainViewModel(platform, useCases);
        await viewModel.InitializeAsync();
        await viewModel.UnlockCommand.ExecuteAsync(null);
        viewModel.SearchText = "demo";

        await viewModel.SearchCommand.ExecuteAsync(null);
        await viewModel.RevealCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasRevealedValue);
        Assert.NotEmpty(viewModel.Results);

        await viewModel.OnBackgroundedAsync();

        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.HasRevealedValue);
        Assert.Empty(viewModel.RevealedValue);
        Assert.Empty(viewModel.Results);
        Assert.Empty(viewModel.SearchText);
        Assert.Equal([false, false, true], platform.CoverStates);
    }

    [Fact]
    public async Task CopyUsesExplicitSelectedSecretAndBoundedTimeout()
    {
        var platform = new Platform();
        using var useCases = new UseCases();
        using var viewModel = new MainViewModel(platform, useCases);
        await viewModel.InitializeAsync();
        await viewModel.UnlockCommand.ExecuteAsync(null);
        await viewModel.SearchCommand.ExecuteAsync(null);

        await viewModel.CopyCommand.ExecuteAsync(null);

        Assert.Equal(useCases.Result.Item.Id, useCases.CopiedItemId);
        Assert.Equal(TimeSpan.FromSeconds(30), useCases.CopyTimeout);
    }

    [Fact]
    public async Task FailedPersistentPrivacyControlPreventsUnlock()
    {
        var platform = new Platform(failPrivacyControls: true);
        using var useCases = new UseCases();
        using var viewModel = new MainViewModel(platform, useCases);

        await viewModel.InitializeAsync();
        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsLocked);
        Assert.False(useCases.Initialized);
        Assert.Contains("unavailable", viewModel.StatusText);
    }

    [Fact]
    public async Task ForegroundExposesOnlyTheLockedUi()
    {
        var platform = new Platform();
        using var useCases = new UseCases();
        using var viewModel = new MainViewModel(platform, useCases);
        await viewModel.InitializeAsync();
        await viewModel.UnlockCommand.ExecuteAsync(null);
        await viewModel.OnBackgroundedAsync();

        await viewModel.OnForegroundedAsync();

        Assert.True(viewModel.IsLocked);
        Assert.Equal([false, false, true, false], platform.CoverStates);
    }

    private sealed class UseCases : IMobileUseCases
    {
        public ConnectedIdentity Identity { get; } = new(
            Guid.NewGuid(),
            MobileUseCases.ProductClientId,
            "account",
            "user@example.test",
            "Demo identity",
            Guid.NewGuid().ToString("D"),
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);

        public SearchResult Result { get; }
        public bool Initialized { get; private set; }
        public Guid? CopiedItemId { get; private set; }
        public TimeSpan? CopyTimeout { get; private set; }

        public UseCases()
        {
            var vault = new VaultResource(
                Guid.NewGuid(),
                "/subscriptions/demo/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/demo",
                "demo-vault",
                "tenant",
                "subscription",
                "rg",
                "eastus",
                new Dictionary<string, string>(),
                new Uri("https://demo-vault.vault.azure.net/"),
                DateTimeOffset.UtcNow);
            var item = new VaultItem(
                Guid.NewGuid(),
                vault.Id,
                "demo-secret",
                VaultObjectType.Secret,
                true,
                new Dictionary<string, string>(),
                "text/plain",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                string.Empty,
                "fingerprint",
                DateTimeOffset.UtcNow);
            Result = new SearchResult(
                item,
                vault,
                Identity.DisplayName,
                "Demo tenant",
                false,
                null,
                false);
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            Initialized = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>([Identity]);

        public Task<ConnectedIdentity> ConnectIdentityAsync(
            string displayName,
            CancellationToken cancellationToken) =>
            Task.FromResult(Identity);

        public Task<SyncRun> SynchronizeAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SyncRun(
                Guid.NewGuid(),
                identity.DisplayName,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                SyncStatus.Completed,
                1,
                1,
                []));

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string text,
            Guid? identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([Result]);

        public Task<SensitiveValue> RetrieveAsync(
            Guid itemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SensitiveValue("temporary-value"));

        public Task CopyAsync(
            Guid itemId,
            TimeSpan clearAfter,
            CancellationToken cancellationToken)
        {
            CopiedItemId = itemId;
            CopyTimeout = clearAfter;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class Platform(bool failPrivacyControls = false)
        : IMobilePlatformServices
    {
        private readonly Verification _verification = new();
        private readonly Identity _identity = new();

        public List<bool> CoverStates { get; } = [];

        public MobilePlatformCapabilities Capabilities { get; } = new(
            MobilePlatformKind.Android,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            false);

        public string DataDirectory => Path.GetTempPath();
        public IKeyMaterialProvider KeyMaterialProvider { get; } = new Keys();
        public IUserVerificationService UserVerificationService => _verification;
        public IClipboardService ClipboardService { get; } = new Clipboard();
        public IIdentityProvider IdentityProvider => _identity;
        public IAzureCredentialProvider AzureCredentialProvider => _identity;

        public Task ApplyPersistentPrivacyControlsAsync(
            CancellationToken cancellationToken) =>
            failPrivacyControls
                ? throw new PlatformNotSupportedException(
                    "Persistent controls are unavailable.")
                : Task.CompletedTask;

        public Task SetSensitiveUiCoveredAsync(
            bool covered,
            CancellationToken cancellationToken)
        {
            CoverStates.Add(covered);
            return Task.CompletedTask;
        }
    }

    private sealed class Verification : IUserVerificationService
    {
        public bool IsAvailable => true;

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken) =>
            Task.FromResult(UserVerificationResult.Verified);
    }

    private sealed class Keys : IKeyMaterialProvider
    {
        public bool IsAvailable => true;

        public Task<byte[]> GetOrCreateKeyAsync(
            string purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult(new byte[32]);

        public Task<byte[]> GetExistingKeyAsync(
            string purpose,
            CancellationToken cancellationToken) =>
            Task.FromResult(new byte[32]);
    }

    private sealed class Clipboard : IClipboardService
    {
        public Task CopyWithAutoClearAsync(
            SensitiveValue value,
            TimeSpan clearAfter,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Identity : IIdentityProvider, IAzureCredentialProvider
    {
        public Task<ConnectedIdentity> SignInAsync(
            string clientId,
            string displayName,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ConnectedIdentity> ReauthenticateAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<TokenCredential> GetCredentialAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
