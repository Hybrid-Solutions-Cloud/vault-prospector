using VaultProspector.Application;
using VaultProspector.Mobile.Core;

namespace VaultProspector.Mobile.Tests;

public sealed class MobileSessionCoordinatorTests
{
    [Fact]
    public async Task UnlockAndBackgroundLockCoverUiAndCancelOperations()
    {
        var covers = new List<bool>();
        using var coordinator = new MobileSessionCoordinator(
            new Verification(UserVerificationResult.Verified),
            (covered, _) =>
            {
                covers.Add(covered);
                return Task.CompletedTask;
            });

        Assert.True(await coordinator.UnlockAsync(CancellationToken.None));
        using var operation = coordinator.BeginSensitiveOperation(
            CancellationToken.None);

        await coordinator.LockAsync(CancellationToken.None);

        Assert.True(coordinator.IsLocked);
        Assert.True(operation.CancellationToken.IsCancellationRequested);
        Assert.Equal([false, true], covers);
    }

    [Fact]
    public async Task DeniedVerificationStaysCoveredAndLocked()
    {
        var coverCalls = 0;
        using var coordinator = new MobileSessionCoordinator(
            new Verification(UserVerificationResult.Canceled),
            (_, _) =>
            {
                coverCalls++;
                return Task.CompletedTask;
            });

        Assert.False(await coordinator.UnlockAsync(CancellationToken.None));
        Assert.True(coordinator.IsLocked);
        Assert.Equal(0, coverCalls);
        Assert.Throws<InvalidOperationException>(
            () => coordinator.BeginSensitiveOperation(
                CancellationToken.None));
    }

    private sealed class Verification(UserVerificationResult result)
        : IUserVerificationService
    {
        public bool IsAvailable => true;

        public Task<UserVerificationResult> VerifyAsync(
            string reason,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
