using VaultProspector.Application;

namespace VaultProspector.Mobile.Core;

public sealed class MobileSessionCoordinator(
    IUserVerificationService verification,
    Func<bool, CancellationToken, Task> setSensitiveUiCovered) : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource _sessionCancellation = new();
    private bool _disposed;

    public bool IsLocked { get; private set; } = true;

    public event EventHandler? StateChanged;

    public async Task<bool> UnlockAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!verification.IsAvailable)
            return false;

        var result = await verification.VerifyAsync(
            "Unlock Vault Prospector on this device",
            cancellationToken);
        if (result != UserVerificationResult.Verified)
            return false;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _sessionCancellation.Dispose();
            _sessionCancellation = new CancellationTokenSource();
            IsLocked = false;
        }

        await setSensitiveUiCovered(false, cancellationToken);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task LockAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancellationTokenSource toCancel;
        lock (_gate)
        {
            if (IsLocked)
                return;

            IsLocked = true;
            toCancel = _sessionCancellation;
            _sessionCancellation = new CancellationTokenSource();
        }

        toCancel.Cancel();
        toCancel.Dispose();
        await setSensitiveUiCovered(true, cancellationToken);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public MobileSensitiveOperationLease BeginSensitiveOperation(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (IsLocked)
                throw new InvalidOperationException("The mobile application is locked.");

            return new MobileSensitiveOperationLease(
                CancellationTokenSource.CreateLinkedTokenSource(
                    _sessionCancellation.Token,
                    cancellationToken));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            IsLocked = true;
            _sessionCancellation.Cancel();
            _sessionCancellation.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

public sealed class MobileSensitiveOperationLease(
    CancellationTokenSource cancellation) : IDisposable
{
    private CancellationTokenSource? _cancellation = cancellation;

    public CancellationToken CancellationToken
    {
        get
        {
            ObjectDisposedException.ThrowIf(_cancellation is null, this);
            return _cancellation.Token;
        }
    }

    public void Dispose()
    {
        _cancellation?.Dispose();
        _cancellation = null;
        GC.SuppressFinalize(this);
    }
}
