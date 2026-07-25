using VaultProspector.Application;

namespace VaultProspector.Application.Tests;

public sealed class RevealVerificationSessionTests
{
    [Fact]
    public async Task OffRequiresVerificationForEveryReveal()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Verified,
            UserVerificationResult.Verified);
        var session = CreateSession(verification);

        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken));
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.Zero,
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task MonotonicGraceReusesThenExpiresAtBoundary()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Verified,
            UserVerificationResult.Verified);
        var time = new ManualTimeProvider();
        var session = CreateSession(verification, timeProvider: time);

        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task EnterpriseCapShortensUserGrace()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Verified,
            UserVerificationResult.Verified);
        var time = new ManualTimeProvider();
        var policy = new MutableEnterprisePolicy(
            new EnterprisePolicySnapshot(
                true,
                maximumRevealVerificationGracePeriod:
                    TimeSpan.FromSeconds(30)));
        var session = CreateSession(verification, policy, time);

        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken));
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task PolicyChangeImmediatelyInvalidatesGrace()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Verified,
            UserVerificationResult.Verified);
        var policy = new MutableEnterprisePolicy(
            EnterprisePolicySnapshot.Unmanaged);
        var session = CreateSession(verification, policy);

        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken));
        policy.Snapshot = new EnterprisePolicySnapshot(
            true,
            allowedTenantIds:
            [
                "11111111-1111-1111-1111-111111111111",
            ]);
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(120),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task ExplicitInvalidationForcesNewVerification()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Verified,
            UserVerificationResult.Verified);
        var session = CreateSession(verification);

        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));
        session.Invalidate();
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task FailedVerificationDoesNotCreateGrace()
    {
        var verification = new SequenceVerificationService(
            UserVerificationResult.Canceled,
            UserVerificationResult.Verified);
        var session = CreateSession(verification);

        Assert.False(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));
        Assert.True(await session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken));

        Assert.Equal(2, verification.CallCount);
    }

    [Fact]
    public async Task ConcurrentRevealsShareOneSuccessfulVerification()
    {
        var verification = new BlockingVerificationService();
        var session = CreateSession(verification);

        var first = session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken);
        await verification.Started.Task;
        var second = session.EnsureVerifiedAsync(
            TimeSpan.FromSeconds(60),
            TestContext.Current.CancellationToken);
        verification.Complete();

        Assert.True(await first);
        Assert.True(await second);
        Assert.Equal(1, verification.CallCount);
    }

    private static RevealVerificationSession CreateSession(
        IUserVerificationService verification,
        IEnterprisePolicy? policy = null,
        TimeProvider? timeProvider = null) =>
        new(
            verification,
            policy ?? UnmanagedEnterprisePolicy.Instance,
            timeProvider);

    private sealed class SequenceVerificationService(
        params UserVerificationResult[] results) :
        IUserVerificationService
    {
        public bool IsAvailable => true;
        public int CallCount { get; private set; }

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Math.Min(CallCount, results.Length - 1);
            CallCount++;
            return Task.FromResult(results[index]);
        }
    }

    private sealed class BlockingVerificationService :
        IUserVerificationService
    {
        private readonly TaskCompletionSource<UserVerificationResult>
            _completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsAvailable => true;
        public int CallCount { get; private set; }
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.TrySetResult();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete() =>
            _completion.TrySetResult(UserVerificationResult.Verified);
    }

    private sealed class MutableEnterprisePolicy(
        EnterprisePolicySnapshot snapshot) : IEnterprisePolicy
    {
        public EnterprisePolicySnapshot Snapshot { get; set; } = snapshot;

        public EnterprisePolicySnapshot GetSnapshot() => Snapshot;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) =>
            _timestamp += elapsed.Ticks;
    }
}
