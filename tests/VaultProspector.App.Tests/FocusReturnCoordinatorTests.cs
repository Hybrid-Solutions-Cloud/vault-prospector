using VaultProspector.App.Views;

namespace VaultProspector.App.Tests;

public sealed class FocusReturnCoordinatorTests
{
    [Fact]
    public void CompletedOperationRestoresTheCapturedEligibleTarget()
    {
        var coordinator = new FocusReturnCoordinator<object>();
        var target = new object();

        coordinator.OperationStarted(target);
        coordinator.OperationCompleted();

        Assert.Same(target, coordinator.TakeRestoreTarget(true, _ => true));
        Assert.False(coordinator.IsRestorePending);
    }

    [Fact]
    public void CompletedOperationWaitsUntilTheHostIsActive()
    {
        var coordinator = new FocusReturnCoordinator<object>();
        var target = new object();
        coordinator.OperationStarted(target);
        coordinator.OperationCompleted();

        Assert.Null(coordinator.TakeRestoreTarget(false, _ => true));
        Assert.True(coordinator.IsRestorePending);
        Assert.Same(target, coordinator.TakeRestoreTarget(true, _ => true));
    }

    [Fact]
    public void IneligibleTargetIsDiscardedWithoutASecondRestoreAttempt()
    {
        var coordinator = new FocusReturnCoordinator<object>();
        coordinator.OperationStarted(new object());
        coordinator.OperationCompleted();

        Assert.Null(coordinator.TakeRestoreTarget(true, _ => false));
        Assert.False(coordinator.IsRestorePending);
        Assert.Null(coordinator.TakeRestoreTarget(true, _ => true));
    }

    [Fact]
    public void LastRememberedTargetIsUsedWhenFocusTemporarilyDisappears()
    {
        var coordinator = new FocusReturnCoordinator<object>();
        var target = new object();
        coordinator.Remember(target);

        coordinator.OperationStarted(null);
        coordinator.OperationCompleted();

        Assert.Same(target, coordinator.TakeRestoreTarget(true, _ => true));
    }
}
