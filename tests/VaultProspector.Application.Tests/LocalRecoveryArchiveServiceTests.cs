using VaultProspector.Application;

namespace VaultProspector.Application.Tests;

public sealed class LocalRecoveryArchiveServiceTests
{
    private const string ArchiveId =
        "20260723-120102003-11111111111111111111111111111111";

    [Fact]
    public async Task IncorrectConfirmationDoesNotPromptOrDelete()
    {
        var verification = new FakeVerification();
        var store = new FakeArchiveStore();
        var service = new LocalRecoveryArchiveService(
            verification,
            store,
            new FakeDiagnostics());

        await Assert.ThrowsAsync<LocalRecoveryArchiveConfirmationException>(
            () => service.DeleteAsync(
                ArchiveId,
                "delete archive",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, store.DeleteCalls);
    }

    [Theory]
    [InlineData(UserVerificationResult.Canceled)]
    [InlineData(UserVerificationResult.Unavailable)]
    [InlineData(UserVerificationResult.NotConfigured)]
    [InlineData(UserVerificationResult.DisabledByPolicy)]
    [InlineData(UserVerificationResult.Failed)]
    public async Task NonVerifiedResultPreservesArchive(
        UserVerificationResult verificationResult)
    {
        var verification = new FakeVerification
        {
            Result = verificationResult,
        };
        var store = new FakeArchiveStore();
        var service = new LocalRecoveryArchiveService(
            verification,
            store,
            new FakeDiagnostics());

        await Assert.ThrowsAsync<LocalRecoveryArchiveVerificationException>(
            () => service.DeleteAsync(
                ArchiveId,
                LocalRecoveryArchiveService.ConfirmationPhrase,
                TestContext.Current.CancellationToken));

        Assert.Equal(1, verification.Calls);
        Assert.Equal(0, store.DeleteCalls);
    }

    [Fact]
    public async Task VerifiedExactConfirmationDeletesAndAudits()
    {
        var verification = new FakeVerification();
        var store = new FakeArchiveStore();
        var diagnostics = new FakeDiagnostics();
        var service = new LocalRecoveryArchiveService(
            verification,
            store,
            diagnostics);

        await service.DeleteAsync(
            ArchiveId,
            $" {LocalRecoveryArchiveService.ConfirmationPhrase} ",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, store.DeleteCalls);
        Assert.Equal(ArchiveId, store.DeletedArchiveId);
        Assert.Equal(
            "local_recovery_archive_deleted",
            diagnostics.EventName);
        Assert.Equal(
            "deleted",
            diagnostics.Fields?["status"]);
    }

    [Fact]
    public async Task AuditFailureBeforeDeletionPreservesArchive()
    {
        var store = new FakeArchiveStore();
        var diagnostics = new FakeDiagnostics
        {
            ThrowOnInformation = true,
        };
        var service = new LocalRecoveryArchiveService(
            new FakeVerification(),
            store,
            diagnostics);

        await Assert.ThrowsAsync<IOException>(
            () => service.DeleteAsync(
                ArchiveId,
                LocalRecoveryArchiveService.ConfirmationPhrase,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, store.DeleteCalls);
    }

    [Fact]
    public async Task ListDelegatesToArchiveStore()
    {
        var expected = new[]
        {
            new LocalRecoveryArchive(
                ArchiveId,
                LocalRecoveryArchiveKind.Reset,
                DateTimeOffset.UnixEpoch,
                42),
        };
        var store = new FakeArchiveStore
        {
            Archives = expected,
        };
        var service = new LocalRecoveryArchiveService(
            new FakeVerification(),
            store,
            new FakeDiagnostics());

        var actual = await service.ListAsync(
            TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    private sealed class FakeVerification : IUserVerificationService
    {
        public bool IsAvailable { get; init; } = true;
        public UserVerificationResult Result { get; init; } =
            UserVerificationResult.Verified;
        public int Calls { get; private set; }

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeArchiveStore : ILocalRecoveryArchiveStore
    {
        public IReadOnlyList<LocalRecoveryArchive> Archives { get; init; } =
            [];
        public int DeleteCalls { get; private set; }
        public string? DeletedArchiveId { get; private set; }

        public Task<IReadOnlyList<LocalRecoveryArchive>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Archives);

        public Task DeleteAsync(
            string archiveId,
            CancellationToken cancellationToken)
        {
            DeleteCalls++;
            DeletedArchiveId = archiveId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiagnostics : IDiagnosticSink
    {
        public bool ThrowOnInformation { get; init; }
        public string? EventName { get; private set; }
        public IReadOnlyDictionary<string, object?>? Fields
        {
            get;
            private set;
        }

        public void Information(
            string eventName,
            IReadOnlyDictionary<string, object?> fields)
        {
            if (ThrowOnInformation)
                throw new IOException("diagnostic write failed");
            EventName = eventName;
            Fields = fields;
        }

        public void WriteError(
            string eventName,
            Exception exception,
            IReadOnlyDictionary<string, object?> fields)
        {
        }
    }
}
