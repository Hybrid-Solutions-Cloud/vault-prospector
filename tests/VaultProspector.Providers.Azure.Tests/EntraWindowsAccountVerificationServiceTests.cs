using VaultProspector.Application;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class EntraWindowsAccountVerificationServiceTests
{
    [Fact]
    public void AzureAdSidConvertsToEntraObjectId()
    {
        var converted = EntraWindowsAccountVerificationInterop
            .TryConvertAzureAdSidToObjectId(
                "S-1-12-1-1122867-1719092309-3148519816-4293844428",
                out var objectId);

        Assert.True(converted);
        Assert.Equal(
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            objectId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("S-1-5-21-1-2-3-4")]
    [InlineData("S-1-12-1-1-2-3")]
    [InlineData("S-1-12-1-1-2-3-not-a-number")]
    public void NonAzureAdSidIsRejected(string? sid)
    {
        var converted = EntraWindowsAccountVerificationInterop
            .TryConvertAzureAdSidToObjectId(sid, out var objectId);

        Assert.False(converted);
        Assert.Equal(Guid.Empty, objectId);
    }

    [Fact]
    public async Task PreservesInteractiveVerificationOutcome()
    {
        var interop = new FakeInterop(
            isAvailable: true,
            EntraWindowsAccountVerificationOutcome.Verified());
        var service = new EntraWindowsAccountVerificationService(
            interop);

        var result = await service.VerifyAsync(
            "Unlock Vault Prospector",
            TestContext.Current.CancellationToken);

        Assert.True(service.IsAvailable);
        Assert.Equal(UserVerificationResult.Verified, result);
        Assert.Equal(1, interop.CallCount);
        Assert.Equal("Unlock Vault Prospector", interop.Reason);
    }

    [Fact]
    public async Task WritesOnlyCategoricalInteractiveDiagnostic()
    {
        var diagnostics = new RecordingDiagnosticSink();
        var service = new EntraWindowsAccountVerificationService(
            new FakeInterop(
                isAvailable: true,
                EntraWindowsAccountVerificationOutcome.Failed(
                    "sid_mismatch")),
            diagnostics);

        var result = await service.VerifyAsync(
            "reason that must not be logged",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.RemoteCredentialFailed, result);
        Assert.Equal(
            "windows_remote_verification_completed",
            diagnostics.EventName);
        Assert.Equal("failed", diagnostics.Fields["status"]);
        Assert.Equal("sid_mismatch", diagnostics.Fields["error_category"]);
        Assert.DoesNotContain(
            diagnostics.Fields.Values,
            value => string.Equals(
                value?.ToString(),
                "reason that must not be logged",
                StringComparison.Ordinal));
    }

    private sealed class FakeInterop(
        bool isAvailable,
        EntraWindowsAccountVerificationOutcome outcome)
        : IEntraWindowsAccountVerificationInterop
    {
        public bool IsCurrentAccountEntra { get; } = isAvailable;
        public int CallCount { get; private set; }
        public string? Reason { get; private set; }

        public Task<EntraWindowsAccountVerificationOutcome>
            VerifyCurrentAccountAsync(
                string reason,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Reason = reason;
            return Task.FromResult(outcome);
        }
    }

    private sealed class RecordingDiagnosticSink : IDiagnosticSink
    {
        public string? EventName { get; private set; }
        public IReadOnlyDictionary<string, object?> Fields { get; private set; } =
            new Dictionary<string, object?>();

        public void Information(
            string eventName,
            IReadOnlyDictionary<string, object?> fields)
        {
            EventName = eventName;
            Fields = fields;
        }

        public void WriteError(
            string eventName,
            Exception exception,
            IReadOnlyDictionary<string, object?> fields) =>
            throw new NotSupportedException();
    }
}
