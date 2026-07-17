using VaultProspector.App;
using VaultProspector.Domain;

namespace VaultProspector.App.Tests;

public sealed class ClipboardSecurityTests
{
    [Fact]
    public async Task OwnedClipboardValueIsClearedOnExplicitCleanup()
    {
        var adapter = new FakeClipboardAdapter();
        var clipboard = new AvaloniaClipboardService(adapter);
        using var value = new SensitiveValue("secret-value");
        await clipboard.CopyWithAutoClearAsync(value, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        await clipboard.ClearIfOwnedAsync(TestContext.Current.CancellationToken);

        Assert.Null(adapter.Text);
        Assert.Equal(1, adapter.ClearCalls);
    }

    [Fact]
    public async Task ExplicitCleanupDoesNotClearClipboardChangedByAnotherOwner()
    {
        var adapter = new FakeClipboardAdapter();
        var clipboard = new AvaloniaClipboardService(adapter);
        using var value = new SensitiveValue("secret-value");
        await clipboard.CopyWithAutoClearAsync(value, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);
        adapter.Text = "unrelated-value";

        await clipboard.ClearIfOwnedAsync(TestContext.Current.CancellationToken);

        Assert.Equal("unrelated-value", adapter.Text);
        Assert.Equal(0, adapter.ClearCalls);
    }

    [Fact]
    public async Task OlderTimerCannotClearAReplacementLease()
    {
        var adapter = new FakeClipboardAdapter();
        var clipboard = new AvaloniaClipboardService(adapter);
        using var first = new SensitiveValue("first-value");
        using var second = new SensitiveValue("second-value");
        await clipboard.CopyWithAutoClearAsync(first, TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        await clipboard.CopyWithAutoClearAsync(second, TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        Assert.Equal("second-value", adapter.Text);
        Assert.Equal(0, adapter.ClearCalls);
    }

    private sealed class FakeClipboardAdapter : ITextClipboardAdapter
    {
        public string? Text { get; set; }
        public int ClearCalls { get; private set; }

        public Task SetTextAsync(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }

        public Task<string?> TryGetTextAsync() => Task.FromResult(Text);

        public Task ClearAsync()
        {
            ClearCalls++;
            Text = null;
            return Task.CompletedTask;
        }
    }
}
