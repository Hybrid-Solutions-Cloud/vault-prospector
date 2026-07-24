using System.Security.Cryptography;
using System.Text;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Mobile.iOS;

public sealed class IosClipboardService : IClipboardService
{
    private readonly object _gate = new();
    private nint _ownedChangeCount = -1;
    private byte[]? _ownedHash;
    private long _ownedGeneration;

    public Task CopyWithAutoClearAsync(
        SensitiveValue value,
        TimeSpan clearAfter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (clearAfter < TimeSpan.FromSeconds(1) ||
            clearAfter > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(clearAfter),
                "Clipboard clearing must be between 1 second and 5 minutes.");
        }
        cancellationToken.ThrowIfCancellationRequested();

        var text = value.Reveal();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        using var key = new NSString(UTTypes.PlainText.Identifier);
        using var content = new NSString(text);
        using var item = new NSDictionary<NSString, NSObject>(key, content);
        var options = new UIPasteboardOptions
        {
            LocalOnly = true,
            ExpirationDate = NSDate.FromTimeIntervalSinceNow(
                clearAfter.TotalSeconds),
        };
        UIPasteboard.General.SetItems(
            [item],
            options);

        long generation;
        lock (_gate)
        {
            if (_ownedHash is not null)
                CryptographicOperations.ZeroMemory(_ownedHash);
            _ownedHash = hash;
            _ownedChangeCount = UIPasteboard.General.ChangeCount;
            _ownedGeneration++;
            generation = _ownedGeneration;
        }

        _ = ClearAfterAsync(clearAfter, generation);
        return Task.CompletedTask;
    }

    public Task ClearIfOwnedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClearIfOwned();
        return Task.CompletedTask;
    }

    private async Task ClearAfterAsync(TimeSpan delay, long generation)
    {
        try
        {
            await Task.Delay(delay);
            await MainThreadAsync(() => ClearIfOwned(generation));
        }
        catch
        {
            // The lifecycle lock path invokes ClearIfOwnedAsync again.
        }
    }

    private void ClearIfOwned() => ClearIfOwned(null);

    private void ClearIfOwned(long? expectedGeneration)
    {
        byte[]? expected;
        nint changeCount;
        long generation;
        lock (_gate)
        {
            if (expectedGeneration.HasValue &&
                expectedGeneration.Value != _ownedGeneration)
                return;
            expected = _ownedHash?.ToArray();
            changeCount = _ownedChangeCount;
            generation = _ownedGeneration;
        }
        if (expected is null)
            return;

        try
        {
            var pasteboard = UIPasteboard.General;
            var current = pasteboard.String;
            if (pasteboard.ChangeCount != changeCount || current is null)
                return;
            var currentHash = SHA256.HashData(
                Encoding.UTF8.GetBytes(current));
            try
            {
                if (CryptographicOperations.FixedTimeEquals(
                    currentHash,
                    expected))
                {
                    pasteboard.Items = [];
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(currentHash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            lock (_gate)
            {
                if (generation == _ownedGeneration)
                {
                    if (_ownedHash is not null)
                        CryptographicOperations.ZeroMemory(_ownedHash);
                    _ownedHash = null;
                    _ownedChangeCount = -1;
                }
            }
        }
    }

    private static Task MainThreadAsync(Action action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }
}
