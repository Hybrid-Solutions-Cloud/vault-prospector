using System.Security.Cryptography;
using System.Text;
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
    private byte[]? _ownedDigest;
    private long _leaseId;

    public AvaloniaClipboardService() : this(new AvaloniaTextClipboardAdapter()) { }

    public AvaloniaClipboardService(ITextClipboardAdapter clipboard) =>
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

    public async Task CopyWithAutoClearAsync(SensitiveValue value, TimeSpan clearAfter, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(clearAfter, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        var copiedText = value.Reveal();
        byte[]? copiedDigest = Digest(copiedText);
        try
        {
            var leaseId = await RunExclusiveAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _clipboard.SetTextAsync(copiedText);
                ReplaceOwnedDigest(copiedDigest);
                copiedDigest = null;
                return ++_leaseId;
            });

            _ = ClearLaterAsync(leaseId, clearAfter);
        }
        finally
        {
            if (copiedDigest is not null) CryptographicOperations.ZeroMemory(copiedDigest);
        }
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
        if (_ownedDigest is null || (expectedLeaseId is not null && expectedLeaseId.Value != _leaseId)) return;
        var current = await _clipboard.TryGetTextAsync();
        if (current is not null)
        {
            var currentDigest = Digest(current);
            try
            {
                if (CryptographicOperations.FixedTimeEquals(currentDigest, _ownedDigest))
                    await _clipboard.ClearAsync();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(currentDigest);
            }
        }

        ClearOwnedDigest();
    }

    private void ReplaceOwnedDigest(byte[] digest)
    {
        ClearOwnedDigest();
        _ownedDigest = digest;
    }

    private void ClearOwnedDigest()
    {
        if (_ownedDigest is null) return;
        CryptographicOperations.ZeroMemory(_ownedDigest);
        _ownedDigest = null;
    }

    private static byte[] Digest(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
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
