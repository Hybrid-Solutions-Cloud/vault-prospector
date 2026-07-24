using System.Text;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class WindowsCyberArkCredentialStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"vault-prospector-cyberark-{Guid.NewGuid():N}");

    [Fact]
    public async Task RoundTripProtectsCredentialAndRemovalIsExact()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var store = new WindowsCyberArkCredentialStore(_directory);
        var profileId = Guid.NewGuid();
        using var credential = new SensitiveValue(
            "synthetic-cyberark-client-credential");

        await store.StoreAsync(
            profileId,
            credential,
            TestContext.Current.CancellationToken);

        var path = Path.Combine(
            _directory,
            $"{profileId:D}.credential");
        var bytes = await File.ReadAllBytesAsync(
            path,
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            "synthetic-cyberark-client-credential",
            Encoding.UTF8.GetString(bytes),
            StringComparison.Ordinal);
        using var restored = await store.RetrieveAsync(
            profileId,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "synthetic-cyberark-client-credential",
            restored.Reveal());

        await store.RemoveAsync(
            profileId,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(path));
        await Assert.ThrowsAsync<ProtectedKeyUnavailableException>(
            () => store.RetrieveAsync(
                profileId,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CredentialCannotBeMovedAcrossProfiles()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var store = new WindowsCyberArkCredentialStore(_directory);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using var credential = new SensitiveValue("synthetic");
        await store.StoreAsync(
            first,
            credential,
            TestContext.Current.CancellationToken);
        Directory.CreateDirectory(_directory);
        File.Copy(
            Path.Combine(_directory, $"{first:D}.credential"),
            Path.Combine(_directory, $"{second:D}.credential"));

        await Assert.ThrowsAsync<LocalDataIntegrityException>(
            () => store.RetrieveAsync(
                second,
                TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }
}
