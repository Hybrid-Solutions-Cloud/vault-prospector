namespace VaultProspector.App.Views;

internal sealed class FocusReturnCoordinator<T> where T : class
{
    private WeakReference<T>? _lastFocusedTarget;
    private WeakReference<T>? _operationTarget;

    public bool IsRestorePending { get; private set; }

    public void Remember(T target) => _lastFocusedTarget = new WeakReference<T>(target);

    public void OperationStarted(T? focusedTarget)
    {
        _operationTarget = focusedTarget is not null
            ? new WeakReference<T>(focusedTarget)
            : _lastFocusedTarget;
        IsRestorePending = false;
    }

    public void OperationCompleted() => IsRestorePending = true;

    public void OperationFailed()
    {
        IsRestorePending = false;
    }

    public void RequestRestore() => IsRestorePending = _operationTarget is not null;

    public T? TakeRestoreTarget(bool hostIsActive, Func<T, bool> canRestore)
    {
        if (!IsRestorePending || !hostIsActive) return null;

        IsRestorePending = false;
        if (_operationTarget is null ||
            !_operationTarget.TryGetTarget(out var target) ||
            !canRestore(target))
        {
            _operationTarget = null;
            return null;
        }

        _operationTarget = null;
        return target;
    }
}
