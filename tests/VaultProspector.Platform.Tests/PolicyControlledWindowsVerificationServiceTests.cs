using VaultProspector.Application;
using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class PolicyControlledWindowsVerificationServiceTests
{
    [Fact]
    public async Task LocalConsoleNeverUsesRemoteFallback()
    {
        var remote = new FixedVerificationService(
            UserVerificationResult.Verified);
        var service = Create(
            UserVerificationResult.Unavailable,
            remote,
            isRemoteSession: false);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Unavailable, result);
        Assert.Equal(0, remote.CallCount);
    }

    [Fact]
    public async Task RemoteSessionUsesAllowedCredentialFallback()
    {
        var remote = new FixedVerificationService(
            UserVerificationResult.Verified);
        var service = Create(
            UserVerificationResult.RemoteSessionUnavailable,
            remote,
            isRemoteSession: true);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.Verified, result);
        Assert.Equal(1, remote.CallCount);
    }

    [Fact]
    public async Task MachinePolicyCanDenyRemoteCredentialFallback()
    {
        var remote = new FixedVerificationService(
            UserVerificationResult.Verified);
        var policy = new StaticEnterprisePolicy(
            new EnterprisePolicySnapshot(
                true,
                allowRemoteCredentialVerification: false));
        var service = new PolicyControlledWindowsVerificationService(
            new FixedVerificationService(
                UserVerificationResult.RemoteSessionUnavailable),
            remote,
            policy,
            () => true);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.DisabledByPolicy, result);
        Assert.Equal(0, remote.CallCount);
    }

    [Theory]
    [InlineData(UserVerificationResult.Canceled)]
    [InlineData(UserVerificationResult.RemoteCredentialUnavailable)]
    [InlineData(UserVerificationResult.RemoteCredentialFailed)]
    public async Task RemoteFallbackFailsClosedWithProviderOutcome(
        UserVerificationResult providerResult)
    {
        var service = Create(
            UserVerificationResult.RemoteSessionUnavailable,
            new FixedVerificationService(providerResult),
            isRemoteSession: true);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(providerResult, result);
    }

    [Fact]
    public async Task InvalidMachinePolicyDeniesRemoteFallback()
    {
        var service = new PolicyControlledWindowsVerificationService(
            new FixedVerificationService(
                UserVerificationResult.RemoteSessionUnavailable),
            new FixedVerificationService(
                UserVerificationResult.Verified),
            new StaticEnterprisePolicy(
                EnterprisePolicySnapshot.Invalid("test invalid policy")),
            () => true);

        var result = await service.VerifyAsync(
            "Verify",
            TestContext.Current.CancellationToken);

        Assert.Equal(UserVerificationResult.DisabledByPolicy, result);
    }

    private static PolicyControlledWindowsVerificationService Create(
        UserVerificationResult localResult,
        FixedVerificationService remote,
        bool isRemoteSession) =>
        new(
            new FixedVerificationService(localResult),
            remote,
            UnmanagedEnterprisePolicy.Instance,
            () => isRemoteSession);

    private sealed class FixedVerificationService(
        UserVerificationResult result,
        bool isAvailable = true) : IUserVerificationService
    {
        public int CallCount { get; private set; }
        public bool IsAvailable { get; } = isAvailable;

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class StaticEnterprisePolicy(
        EnterprisePolicySnapshot snapshot) : IEnterprisePolicy
    {
        public EnterprisePolicySnapshot GetSnapshot() => snapshot;
    }
}
