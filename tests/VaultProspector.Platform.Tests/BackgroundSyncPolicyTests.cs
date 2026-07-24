using VaultProspector.Platform;

namespace VaultProspector.Platform.Tests;

public sealed class BackgroundSyncPolicyTests
{
    [Theory]
    [InlineData(false, true, true, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, true, true, false)]
    public void AnyUnmetConstraintBlocksBackgroundSynchronization(
        bool enabled,
        bool hidden,
        bool network,
        bool powerKnown,
        bool externalPower)
    {
        Assert.False(BackgroundSyncPolicy.IsEligible(
            enabled,
            hidden,
            network,
            powerKnown,
            externalPower));
    }

    [Fact]
    public void ExplicitOptInHiddenNetworkAndExternalPowerAllowBackgroundSynchronization()
    {
        Assert.True(BackgroundSyncPolicy.IsEligible(
            enabled: true,
            mainWindowHidden: true,
            networkAvailable: true,
            powerStatusKnown: true,
            onExternalPower: true));
    }
}
