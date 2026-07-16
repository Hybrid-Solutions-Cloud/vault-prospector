using VaultProspector.Domain;

namespace VaultProspector.Domain.Tests;

public sealed class SecurityPolicyTests
{
    [Fact]
    public void SecureDefaultDisablesOfflineCaching() => Assert.False(CachePolicy.SecureDefault.IsEnabled);

    [Fact]
    public void CacheExpirationIsCappedByPolicy()
    {
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var policy = new CachePolicy(true, TimeSpan.FromHours(8), true, true);
        Assert.Equal(now.AddHours(8), policy.GetExpiration(now, TimeSpan.FromDays(2)));
    }

    [Fact]
    public void DisabledCacheFailsClosed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CachePolicy.SecureDefault.GetExpiration(DateTimeOffset.UtcNow, TimeSpan.FromHours(1)));
        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SensitiveValueNeverRevealsThroughToStringAndCannotBeReadAfterDispose()
    {
        var value = new SensitiveValue("highly-sensitive-value");
        Assert.Equal("[REDACTED]", value.ToString());
        Assert.DoesNotContain("sensitive", value.Mask(), StringComparison.OrdinalIgnoreCase);
        value.Dispose();
        Assert.True(value.IsDisposed);
        Assert.Throws<ObjectDisposedException>(value.Reveal);
    }
}
