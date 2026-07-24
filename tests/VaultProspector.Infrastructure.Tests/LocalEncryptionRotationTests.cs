using System.Security.Cryptography;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Infrastructure;

namespace VaultProspector.Infrastructure.Tests;

public sealed class LocalEncryptionRotationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"vault-prospector-rotation-{Guid.NewGuid():N}");
    private string DataDirectory => Path.Combine(_root, "data");
    private string DatabasePath => Path.Combine(
        DataDirectory,
        "vault-prospector.db");
    private string CacheDirectory => Path.Combine(
        DataDirectory,
        "protected-values");
    private string KeyDirectory => Path.Combine(
        DataDirectory,
        "keys");

    [Fact]
    public async Task SuccessfulRotationRekeysDatabaseAndEveryOfflineValue()
    {
        var fixture = await CreateFixtureAsync();
        var oldMetadataKey = await fixture.Keys.GetExistingKeyAsync(
            "metadata-database",
            TestContext.Current.CancellationToken);
        var oldOfflineKey = await fixture.Keys.GetExistingKeyAsync(
            "offline-values-v2",
            TestContext.Current.CancellationToken);

        var result = await fixture.Engine.RotateAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.OfflineValuesRotated);
        Assert.True(Directory.Exists(result.RecoveryArchivePath));
        Assert.False(File.Exists(Path.Combine(
            DataDirectory,
            "local-encryption.rotation")));
        Assert.Empty(Directory.GetFiles(
            KeyDirectory,
            "*.next"));
        Assert.Empty(Directory.GetFiles(
            KeyDirectory,
            "*.previous"));

        var newMetadataKey = await fixture.Keys.GetExistingKeyAsync(
            "metadata-database",
            TestContext.Current.CancellationToken);
        var newOfflineKey = await fixture.Keys.GetExistingKeyAsync(
            "offline-values-v2",
            TestContext.Current.CancellationToken);
        try
        {
            Assert.NotEqual(oldMetadataKey, newMetadataKey);
            Assert.NotEqual(oldOfflineKey, newOfflineKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newMetadataKey);
            CryptographicOperations.ZeroMemory(newOfflineKey);
        }

        var repository = new EncryptedSqliteMetadataRepository(
            DatabasePath,
            fixture.Keys);
        await repository.InitializeAsync(
            TestContext.Current.CancellationToken);
        Assert.NotNull(await repository.GetIdentityAsync(
            fixture.Identity.Id,
            TestContext.Current.CancellationToken));

        var store = new EncryptedFileValueStore(
            CacheDirectory,
            fixture.Keys,
            fixture.Clock);
        using var restored = await store.RetrieveAsync(
            fixture.ItemId,
            fixture.Clock.UtcNow,
            "fingerprint",
            TestContext.Current.CancellationToken);
        Assert.NotNull(restored);
        Assert.Equal("rotation-canary", restored.Reveal());

        var oldProvider = new FixedMetadataKeyProvider(oldMetadataKey);
        var oldRepository = new EncryptedSqliteMetadataRepository(
            DatabasePath,
            oldProvider);
        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            oldRepository.InitializeAsync(
                TestContext.Current.CancellationToken));

        CryptographicOperations.ZeroMemory(oldMetadataKey);
        CryptographicOperations.ZeroMemory(oldOfflineKey);
    }

    [Theory]
    [InlineData(LocalEncryptionRotationCheckpoint.ArchivePublished)]
    [InlineData(LocalEncryptionRotationCheckpoint.JournalPublished)]
    [InlineData(LocalEncryptionRotationCheckpoint.KeysStaged)]
    [InlineData(LocalEncryptionRotationCheckpoint.DatabaseRekeyed)]
    [InlineData(LocalEncryptionRotationCheckpoint.OfflineValuesReencrypted)]
    [InlineData(LocalEncryptionRotationCheckpoint.MetadataKeyPublished)]
    [InlineData(LocalEncryptionRotationCheckpoint.OfflineKeyPublished)]
    [InlineData(LocalEncryptionRotationCheckpoint.ReplacementValidated)]
    [InlineData(LocalEncryptionRotationCheckpoint.OldKeysRemoved)]
    public async Task EveryPublishedCrashBoundaryRecoversOrLeavesOriginalUntouched(
        LocalEncryptionRotationCheckpoint checkpoint)
    {
        var fixture = await CreateFixtureAsync(
            new ThrowAtCheckpoint(checkpoint));

        await Assert.ThrowsAsync<InjectedRotationFailure>(() =>
            fixture.Engine.RotateAsync(
                TestContext.Current.CancellationToken));

        var recoveryEngine = CreateEngine(
            fixture.Keys,
            fixture.Clock);
        var recovery = await recoveryEngine.RecoverIfNeededAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(
            checkpoint != LocalEncryptionRotationCheckpoint.ArchivePublished,
            recovery.Recovered);

        await AssertOriginalStateReadableAsync(fixture);
    }

    [Fact]
    public async Task CrashInsideKeyPublicationRollsBackFromVerifiedArchive()
    {
        var keys = new FileBackedRotatableKeyProvider(KeyDirectory)
        {
            FailDuringPublishPurpose = "metadata-database",
        };
        var fixture = await CreateFixtureAsync(keys: keys);

        await Assert.ThrowsAsync<InjectedRotationFailure>(() =>
            fixture.Engine.RotateAsync(
                TestContext.Current.CancellationToken));

        keys.FailDuringPublishPurpose = null;
        var recovery = await CreateEngine(keys, fixture.Clock)
            .RecoverIfNeededAsync(TestContext.Current.CancellationToken);

        Assert.True(recovery.Recovered);
        await AssertOriginalStateReadableAsync(fixture);
    }

    [Fact]
    public async Task TamperedJournalFailsClosedWithoutMovingActiveState()
    {
        var fixture = await CreateFixtureAsync(
            new ThrowAtCheckpoint(
                LocalEncryptionRotationCheckpoint.KeysStaged));
        await Assert.ThrowsAsync<InjectedRotationFailure>(() =>
            fixture.Engine.RotateAsync(
                TestContext.Current.CancellationToken));
        var journalPath = Path.Combine(
            DataDirectory,
            "local-encryption.rotation");
        var originalDatabase = await File.ReadAllBytesAsync(
            DatabasePath,
            TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(
            journalPath,
            "tamper",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            CreateEngine(fixture.Keys, fixture.Clock)
                .RecoverIfNeededAsync(
                    TestContext.Current.CancellationToken));

        Assert.True(File.Exists(journalPath));
        Assert.Equal(
            originalDatabase,
            await File.ReadAllBytesAsync(
                DatabasePath,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TamperedArchiveFailsClosedWithoutReplacingActiveState()
    {
        var fixture = await CreateFixtureAsync(
            new ThrowAtCheckpoint(
                LocalEncryptionRotationCheckpoint.DatabaseRekeyed));
        await Assert.ThrowsAsync<InjectedRotationFailure>(() =>
            fixture.Engine.RotateAsync(
                TestContext.Current.CancellationToken));
        var recoveryRoot = Path.Combine(_root, "data-Recovery");
        var archive = Directory.GetDirectories(
            recoveryRoot,
            "rotation-*").Single();
        await File.AppendAllTextAsync(
            Path.Combine(archive, "settings.json"),
            "tamper",
            TestContext.Current.CancellationToken);
        var journalPath = Path.Combine(
            DataDirectory,
            "local-encryption.rotation");

        await Assert.ThrowsAsync<LocalDataIntegrityException>(() =>
            CreateEngine(fixture.Keys, fixture.Clock)
                .RecoverIfNeededAsync(
                    TestContext.Current.CancellationToken));

        Assert.True(File.Exists(journalPath));
        Assert.True(File.Exists(DatabasePath));
    }

    private async Task<RotationFixture> CreateFixtureAsync(
        ILocalEncryptionRotationFaultInjector? faultInjector = null,
        FileBackedRotatableKeyProvider? keys = null)
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(DataDirectory, "settings.json"),
            "{\"test\":true}",
            TestContext.Current.CancellationToken);
        keys ??= new FileBackedRotatableKeyProvider(KeyDirectory);
        var clock = new FixedClock();
        var repository = new EncryptedSqliteMetadataRepository(
            DatabasePath,
            keys);
        await repository.InitializeAsync(
            TestContext.Current.CancellationToken);
        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "account",
            "user@example.invalid",
            "Rotation test",
            "22222222-2222-2222-2222-222222222222",
            AuthenticationState.Ready,
            clock.UtcNow);
        await repository.UpsertIdentityAsync(
            identity,
            TestContext.Current.CancellationToken);

        var itemId = Guid.NewGuid();
        var store = new EncryptedFileValueStore(
            CacheDirectory,
            keys,
            clock);
        using (var value = new SensitiveValue("rotation-canary"))
        {
            await store.StoreAsync(
                itemId,
                Guid.NewGuid(),
                null,
                value,
                "fingerprint",
                clock.UtcNow.AddHours(1),
                TestContext.Current.CancellationToken);
        }

        return new RotationFixture(
            keys,
            clock,
            identity,
            itemId,
            CreateEngine(keys, clock, faultInjector));
    }

    private LocalEncryptionRotationEngine CreateEngine(
        FileBackedRotatableKeyProvider keys,
        IClock clock,
        ILocalEncryptionRotationFaultInjector? faultInjector = null) =>
        new(
            DataDirectory,
            DatabasePath,
            CacheDirectory,
            keys,
            clock,
            faultInjector);

    private async Task AssertOriginalStateReadableAsync(
        RotationFixture fixture)
    {
        var repository = new EncryptedSqliteMetadataRepository(
            DatabasePath,
            fixture.Keys);
        await repository.InitializeAsync(
            TestContext.Current.CancellationToken);
        Assert.NotNull(await repository.GetIdentityAsync(
            fixture.Identity.Id,
            TestContext.Current.CancellationToken));
        var store = new EncryptedFileValueStore(
            CacheDirectory,
            fixture.Keys,
            fixture.Clock);
        using var value = await store.RetrieveAsync(
            fixture.ItemId,
            fixture.Clock.UtcNow,
            "fingerprint",
            TestContext.Current.CancellationToken);
        Assert.NotNull(value);
        Assert.Equal("rotation-canary", value.Reveal());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }

    private sealed record RotationFixture(
        FileBackedRotatableKeyProvider Keys,
        IClock Clock,
        ConnectedIdentity Identity,
        Guid ItemId,
        LocalEncryptionRotationEngine Engine);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow =>
            DateTimeOffset.Parse(
                "2026-07-23T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class ThrowAtCheckpoint(
        LocalEncryptionRotationCheckpoint target) :
        ILocalEncryptionRotationFaultInjector
    {
        public void Reached(
            LocalEncryptionRotationCheckpoint checkpoint)
        {
            if (checkpoint == target)
                throw new InjectedRotationFailure();
        }
    }

    private sealed class InjectedRotationFailure :
        Exception;

    private sealed class FixedMetadataKeyProvider(byte[] key) :
        IKeyMaterialProvider
    {
        public bool IsAvailable => true;

        public Task<byte[]> GetOrCreateKeyAsync(
            string purpose,
            CancellationToken cancellationToken) =>
            GetExistingKeyAsync(purpose, cancellationToken);

        public Task<byte[]> GetExistingKeyAsync(
            string purpose,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (purpose != "metadata-database")
                throw new ProtectedKeyUnavailableException(
                    "No test key exists for this purpose.");
            return Task.FromResult(key.ToArray());
        }
    }

    private sealed class FileBackedRotatableKeyProvider(string directory) :
        IRotatableKeyMaterialProvider
    {
        public bool IsAvailable => true;
        public string? FailDuringPublishPurpose { get; set; }

        public bool HasKey(string purpose) =>
            File.Exists(CurrentPath(purpose));

        public async Task<byte[]> GetOrCreateKeyAsync(
            string purpose,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(directory);
            var path = CurrentPath(purpose);
            if (!File.Exists(path))
            {
                await File.WriteAllBytesAsync(
                    path,
                    RandomNumberGenerator.GetBytes(32),
                    cancellationToken);
            }
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }

        public async Task<byte[]> GetExistingKeyAsync(
            string purpose,
            CancellationToken cancellationToken)
        {
            var path = CurrentPath(purpose);
            if (!File.Exists(path))
                throw new ProtectedKeyUnavailableException(
                    "The test key does not exist.");
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }

        public async Task<StagedKeyRotation> StageReplacementAsync(
            string purpose,
            Guid rotationId,
            CancellationToken cancellationToken)
        {
            if (!HasKey(purpose))
                throw new ProtectedKeyUnavailableException(
                    "The test key does not exist.");
            var key = RandomNumberGenerator.GetBytes(32);
            await File.WriteAllBytesAsync(
                StagedPath(purpose, rotationId),
                key,
                cancellationToken);
            return new StagedKeyRotation(purpose, rotationId, key);
        }

        public Task PublishReplacementAsync(
            StagedKeyRotation rotation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(
                CurrentPath(rotation.Purpose),
                PreviousPath(
                    rotation.Purpose,
                    rotation.RotationId));
            if (rotation.Purpose == FailDuringPublishPurpose)
                throw new InjectedRotationFailure();
            File.Move(
                StagedPath(
                    rotation.Purpose,
                    rotation.RotationId),
                CurrentPath(rotation.Purpose));
            return Task.CompletedTask;
        }

        public Task CompleteReplacementAsync(
            string purpose,
            Guid rotationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delete(PreviousPath(purpose, rotationId));
            Delete(StagedPath(purpose, rotationId));
            return Task.CompletedTask;
        }

        public Task AbortReplacementAsync(
            string purpose,
            Guid rotationId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delete(StagedPath(purpose, rotationId));
            return Task.CompletedTask;
        }

        private string CurrentPath(string purpose) =>
            Path.Combine(directory, $"{purpose}.key");
        private string StagedPath(string purpose, Guid rotationId) =>
            Path.Combine(directory, $"{purpose}.{rotationId:N}.next");
        private string PreviousPath(string purpose, Guid rotationId) =>
            Path.Combine(directory, $"{purpose}.{rotationId:N}.previous");
        private static void Delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
