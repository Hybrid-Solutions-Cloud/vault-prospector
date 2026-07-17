using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.App;

public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly ITextClipboardAdapter _clipboard;
    private readonly object _queueGate = new();
    private Task _operationTail = Task.CompletedTask;
    private string? _ownedText;
    private long _leaseId;

    public AvaloniaClipboardService() : this(new AvaloniaTextClipboardAdapter()) { }

    public AvaloniaClipboardService(ITextClipboardAdapter clipboard) =>
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

    public async Task CopyWithAutoClearAsync(SensitiveValue value, TimeSpan clearAfter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var copiedText = value.Reveal();
        var leaseId = await RunExclusiveAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _clipboard.SetTextAsync(copiedText);
            _ownedText = copiedText;
            return ++_leaseId;
        });

        _ = ClearLaterAsync(leaseId, clearAfter);
    }

    public Task ClearIfOwnedAsync(CancellationToken cancellationToken) =>
        RunExclusiveAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ClearOwnedCoreAsync(expectedLeaseId: null);
            return true;
        });

    private async Task ClearLaterAsync(long expectedLeaseId, TimeSpan clearAfter)
    {
        await Task.Delay(clearAfter);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await RunExclusiveAsync(async () =>
                {
                    await ClearOwnedCoreAsync(expectedLeaseId);
                    return true;
                });
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            catch
            {
                return;
            }
        }
    }

    private async Task ClearOwnedCoreAsync(long? expectedLeaseId)
    {
        if (_ownedText is null || (expectedLeaseId is not null && expectedLeaseId.Value != _leaseId)) return;
        var current = await _clipboard.TryGetTextAsync();
        if (string.Equals(current, _ownedText, StringComparison.Ordinal)) await _clipboard.ClearAsync();
        _ownedText = null;
    }

    private async Task<T> RunExclusiveAsync<T>(Func<Task<T>> operation)
    {
        Task predecessor;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_queueGate)
        {
            predecessor = _operationTail;
            _operationTail = completion.Task;
        }

        try
        {
            await predecessor;
            return await operation();
        }
        finally
        {
            completion.SetResult();
        }
    }
}

public interface ITextClipboardAdapter
{
    Task SetTextAsync(string text);
    Task<string?> TryGetTextAsync();
    Task ClearAsync();
}

internal sealed class AvaloniaTextClipboardAdapter : ITextClipboardAdapter
{
    public Task SetTextAsync(string text) => Resolve().SetTextAsync(text);
    public Task<string?> TryGetTextAsync() => Resolve().TryGetTextAsync();
    public Task ClearAsync() => Resolve().ClearAsync();

    private static Avalonia.Input.Platform.IClipboard Resolve()
    {
        var window = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
            ?? throw new InvalidOperationException("The desktop window is unavailable.");
        return TopLevel.GetTopLevel(window)?.Clipboard ?? throw new InvalidOperationException("The system clipboard is unavailable.");
    }
}
