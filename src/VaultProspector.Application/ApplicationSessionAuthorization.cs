namespace VaultProspector.Application;

public sealed class ApplicationSessionAuthorization
{
    private int _isAuthorized;

    public bool IsAuthorized =>
        Volatile.Read(ref _isAuthorized) == 1;

    public void Authorize() =>
        Interlocked.Exchange(ref _isAuthorized, 1);

    public void Invalidate() =>
        Interlocked.Exchange(ref _isAuthorized, 0);
}
