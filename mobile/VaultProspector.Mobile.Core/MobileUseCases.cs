using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Infrastructure;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Mobile.Core;

public interface IMobileUseCases : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
        CancellationToken cancellationToken);
    Task<ConnectedIdentity> ConnectIdentityAsync(
        string displayName,
        CancellationToken cancellationToken);
    Task<SyncRun> SynchronizeAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string text,
        Guid? identityId,
        CancellationToken cancellationToken);
    Task<SensitiveValue> RetrieveAsync(
        Guid itemId,
        CancellationToken cancellationToken);
    Task CopyAsync(
        Guid itemId,
        TimeSpan clearAfter,
        CancellationToken cancellationToken);
}

public sealed class MobileUseCases : IMobileUseCases
{
    public const string ProductClientId =
        "221af888-1c16-4637-9d45-b6dd2e1e7634";

    private readonly EncryptedSqliteMetadataRepository _repository;
    private readonly IdentityService _identityService;
    private readonly SynchronizationService _synchronizationService;
    private readonly SearchService _searchService;
    private readonly SecretAccessService _secretAccessService;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public MobileUseCases(IMobilePlatformServices platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        MobilePlatformSecurityPolicy.EnsureSupported(platform.Capabilities);

        var dataDirectory = Path.GetFullPath(platform.DataDirectory);
        var metadataPath = Path.Combine(dataDirectory, "metadata.db");
        var cacheDirectory = Path.Combine(dataDirectory, "offline-values");
        var diagnosticsPath = Path.Combine(
            dataDirectory,
            "diagnostics",
            "vault-prospector.log");
        var clock = new SystemClock();
        _repository = new EncryptedSqliteMetadataRepository(
            metadataPath,
            platform.KeyMaterialProvider);
        var valueStore = new EncryptedFileValueStore(
            cacheDirectory,
            platform.KeyMaterialProvider,
            clock);
        var diagnostics = new RedactingDiagnosticSink(diagnosticsPath);
        var vaultProvider = new AzureVaultProvider(
            platform.AzureCredentialProvider);
        _identityService = new IdentityService(
            platform.IdentityProvider,
            _repository,
            diagnostics,
            valueStore);
        _synchronizationService = new SynchronizationService(
            vaultProvider,
            _repository,
            clock,
            diagnostics);
        _searchService = new SearchService(_repository, clock);
        _secretAccessService = new SecretAccessService(
            vaultProvider,
            _repository,
            valueStore,
            platform.ClipboardService,
            platform.UserVerificationService,
            clock);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;
            await _repository.InitializeAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _repository.GetIdentitiesAsync(cancellationToken);
    }

    public Task<ConnectedIdentity> ConnectIdentityAsync(
        string displayName,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _identityService.AddAsync(
            ProductClientId,
            displayName,
            cancellationToken);
    }

    public Task<SyncRun> SynchronizeAsync(
        ConnectedIdentity identity,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(identity);
        return _synchronizationService.SynchronizeAsync(
            identity,
            cancellationToken);
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string text,
        Guid? identityId,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _searchService.SearchAsync(
            new SearchRequest(
                Text: text?.Trim() ?? string.Empty,
                IdentityId: identityId,
                Limit: 100),
            cancellationToken);
    }

    public Task<SensitiveValue> RetrieveAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _secretAccessService.RetrieveAsync(
            itemId,
            cancellationToken);
    }

    public Task CopyAsync(
        Guid itemId,
        TimeSpan clearAfter,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return _secretAccessService.RetrieveAndCopyAsync(
            itemId,
            clearAfter,
            CachePolicy.SecureDefault,
            cancellationToken);
    }

    public void Dispose()
    {
        _initializationGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "The encrypted mobile data store is not initialized.");
    }
}
