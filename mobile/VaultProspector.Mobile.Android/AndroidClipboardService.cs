using System.Security.Cryptography;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Mobile.Android;

public sealed class AndroidClipboardService(Activity activity)
    : IClipboardService
{
    private readonly object _gate = new();
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
            throw new ArgumentOutOfRangeException(
                nameof(clearAfter),
                "Clipboard clearing must be between 1 second and 5 minutes.");
        cancellationToken.ThrowIfCancellationRequested();

        var text = value.Reveal();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var clipboard = GetClipboard();
        var clip = ClipData.NewPlainText("Vault Prospector protected value", text)
            ?? throw new InvalidOperationException(
                "Android could not create clipboard content.");
        var extras = new PersistableBundle();
        extras.PutBoolean("android.content.extra.IS_SENSITIVE", true);
        var description = clip.Description
            ?? throw new InvalidOperationException(
                "Android did not provide a clipboard description.");
        description.Extras = extras;
        clipboard.PrimaryClip = clip;

        long generation;
        lock (_gate)
        {
            if (_ownedHash is not null)
                CryptographicOperations.ZeroMemory(_ownedHash);
            _ownedHash = hash;
            _ownedGeneration++;
            generation = _ownedGeneration;
        }

        _ = ClearAfterAsync(clearAfter, generation);
        return Task.CompletedTask;
    }

    public Task ClearIfOwnedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return RunOnUiThreadAsync(ClearIfOwned);
    }

    private async Task ClearAfterAsync(TimeSpan delay, long generation)
    {
        try
        {
            await Task.Delay(delay);
            await RunOnUiThreadAsync(() => ClearIfOwned(generation));
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
        long generation;
        lock (_gate)
        {
            if (expectedGeneration.HasValue &&
                expectedGeneration.Value != _ownedGeneration)
                return;
            expected = _ownedHash?.ToArray();
            generation = _ownedGeneration;
        }
        if (expected is null)
            return;

        try
        {
            var clipboard = GetClipboard();
            var item = clipboard.PrimaryClip?.GetItemAt(0);
            var current = item?.CoerceToText(activity)?.ToString();
            if (current is null)
                return;
            var currentHash = SHA256.HashData(
                Encoding.UTF8.GetBytes(current));
            try
            {
                if (CryptographicOperations.FixedTimeEquals(
                    currentHash,
                    expected))
                {
                    clipboard.ClearPrimaryClip();
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
                }
            }
        }
    }

    private ClipboardManager GetClipboard() =>
        activity.GetSystemService(Context.ClipboardService) as ClipboardManager
        ?? throw new PlatformNotSupportedException(
            "The Android clipboard service is unavailable.");

    private Task RunOnUiThreadAsync(Action action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
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
