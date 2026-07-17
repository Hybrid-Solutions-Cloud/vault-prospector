using System.Security.Cryptography;
using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsDataProtectionKeyProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-platform-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ConcurrentProvidersPublishAndReturnOneStableKey()
    {
        var providers = Enumerable.Range(0, 32)
            .Select(_ => new WindowsDataProtectionKeyProvider(_directory))
            .ToArray();

        var keys = await Task.WhenAll(providers.Select(x => x.GetOrCreateKeyAsync(
            "metadata-database",
            TestContext.Current.CancellationToken)));

        try
        {
            Assert.All(keys, key => Assert.Equal(keys[0], key));
            Assert.Single(Directory.GetFiles(_directory, "*.key"));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));

            var reloaded = await new WindowsDataProtectionKeyProvider(_directory).GetOrCreateKeyAsync(
                "metadata-database",
                TestContext.Current.CancellationToken);
            try
            {
                Assert.Equal(keys[0], reloaded);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(reloaded);
            }
        }
        finally
        {
            foreach (var key in keys) CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DifferentPurposesUseDifferentProtectedKeys()
    {
        var provider = new WindowsDataProtectionKeyProvider(_directory);
        var metadata = await provider.GetOrCreateKeyAsync("metadata-database", TestContext.Current.CancellationToken);
        var offline = await provider.GetOrCreateKeyAsync("offline-values-v1", TestContext.Current.CancellationToken);

        try
        {
            Assert.NotEqual(metadata, offline);
            Assert.Equal(2, Directory.GetFiles(_directory, "*.key").Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(metadata);
            CryptographicOperations.ZeroMemory(offline);
        }
    }

    [Fact]
    public async Task MissingExistingKeyFailsWithoutCreatingDirectoryOrReplacementKey()
    {
        var provider = new WindowsDataProtectionKeyProvider(_directory);

        await Assert.ThrowsAsync<ProtectedKeyUnavailableException>(() => provider.GetExistingKeyAsync(
            "metadata-database",
            TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("metadata/database")]
    [InlineData("metadata.database")]
    [InlineData("-metadata")]
    [InlineData("Metadata")]
    [InlineData("métadata")]
    [InlineData("con")]
    public async Task InvalidPurposeIsRejectedInsteadOfCollapsingToAnotherKeyPath(string purpose)
    {
        var provider = new WindowsDataProtectionKeyProvider(_directory);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetOrCreateKeyAsync(
            purpose,
            TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }
}
