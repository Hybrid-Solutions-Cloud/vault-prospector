using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class RemoteWindowsCredentialVerificationServiceTests
{
    [Fact]
    public async Task MissingWindowHandleFailsClosedWithoutPrompt()
    {
        var interop = new FakeInterop(
            RemoteWindowsCredentialVerificationOutcome.FromResult(
                UserVerificationResult.Verified));
        var service = new RemoteWindowsCredentialVerificationService(
            () => 0,
            interop);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Unavailable, result);
        Assert.Equal(0, interop.CallCount);
    }

    [Theory]
    [InlineData(UserVerificationResult.Verified)]
    [InlineData(UserVerificationResult.Canceled)]
    [InlineData(UserVerificationResult.RemoteCredentialUnavailable)]
    [InlineData(UserVerificationResult.RemoteCredentialFailed)]
    public async Task PreservesCredentialPromptOutcome(
        UserVerificationResult expected)
    {
        var interop = new FakeInterop(
            RemoteWindowsCredentialVerificationOutcome.FromResult(expected));
        var service = new RemoteWindowsCredentialVerificationService(
            () => 42,
            interop);

        var result = await service.VerifyAsync(
            "Unlock Vault Prospector",
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        Assert.Equal(1, interop.CallCount);
        Assert.Equal(42, interop.WindowHandle);
        Assert.Equal("Unlock Vault Prospector", interop.Reason);
    }

    [Theory]
    [InlineData("person@example.com", null, "AzureAD")]
    [InlineData("KristopherTurner", "", "AzureAD")]
    [InlineData("person@example.com", "AzureAD", "AzureAD")]
    [InlineData("local-user", null, null)]
    public void SuppliesAzureAdDomainForUnqualifiedCredentialInEntraSession(
        string userName,
        string? domain,
        string? expectedDomain)
    {
        var actual =
            RemoteWindowsCredentialInterop.NormalizeEntraLogonName(
                (userName, domain),
                expectedDomain is null
                    ? "WORKSTATION\\local-user"
                    : "AzureAD\\CurrentUser");

        Assert.Equal(userName, actual.UserName);
        Assert.Equal(expectedDomain, actual.Domain);
    }

    [Fact]
    public async Task WritesOnlyCategoricalRemoteVerificationDiagnostic()
    {
        var diagnostics = new RecordingDiagnosticSink();
        var service = new RemoteWindowsCredentialVerificationService(
            () => 42,
            new FakeInterop(
                new RemoteWindowsCredentialVerificationOutcome(
                    UserVerificationResult.RemoteCredentialFailed,
                    "failed",
                    "sid_mismatch")),
            diagnostics);

        var result = await service.VerifyAsync(
            "business reason that must not be logged",
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
                "business reason that must not be logged",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        "WORKSTATION\\local-user",
        null,
        "local-user",
        "WORKSTATION")]
    [InlineData(
        ".\\local-user",
        "",
        "local-user",
        ".")]
    [InlineData(
        "AzureAD\\person@example.com",
        " ",
        "person@example.com",
        "AzureAD")]
    [InlineData(
        "person@example.com",
        null,
        "person@example.com",
        null)]
    [InlineData(
        "provided-user",
        "PROVIDED-DOMAIN",
        "provided-user",
        "PROVIDED-DOMAIN")]
    public void NormalizesQualifiedAccountNamesForLogonUser(
        string userName,
        string? domain,
        string expectedUserName,
        string? expectedDomain)
    {
        var actual =
            RemoteWindowsCredentialInterop.NormalizeLogonName(
                userName,
                domain);

        Assert.Equal(expectedUserName, actual.UserName);
        Assert.Equal(expectedDomain, actual.Domain);
    }

    private sealed class FakeInterop(
        RemoteWindowsCredentialVerificationOutcome outcome) :
        IRemoteWindowsCredentialInterop
    {
        public int CallCount { get; private set; }
        public nint WindowHandle { get; private set; }
        public string? Reason { get; private set; }

        public RemoteWindowsCredentialVerificationOutcome VerifyCurrentUser(
            nint windowHandle,
            string reason)
        {
            CallCount++;
            WindowHandle = windowHandle;
            Reason = reason;
            return outcome;
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
