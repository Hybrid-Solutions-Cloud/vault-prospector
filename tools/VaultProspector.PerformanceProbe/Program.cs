using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Infrastructure;

return await PerformanceProbe.RunAsync(args);

internal static class PerformanceProbe
{
    private const int IdentityCount = 10;
    private const int TenantsPerIdentity = 2;
    private const int SubscriptionsPerIdentity = 20;
    private const int VaultsPerIdentity = 20;
    private const int ItemsPerVault = 250;
    private const int SearchIterations = 60;

    private const double EmptyInitializeLimitMilliseconds = 2_000;
    private const double MetadataSyncLimitMilliseconds = 60_000;
    private const double RepositoryReopenLimitMilliseconds = 5_000;
    private const double SearchP95LimitMilliseconds = 1_000;
    private const double SearchMaximumLimitMilliseconds = 1_500;
    private const double CancellationLimitMilliseconds = 500;
    private const double PrivateMemoryLimitMebibytes = 512;
    private const double DatabaseSizeLimitMebibytes = 256;

    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse(
            "2026-07-24T12:00:00Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    public static async Task<int> RunAsync(string[] args)
    {
        var options = Options.Parse(args);
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-performance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        EncryptedSqliteMetadataRepository? repository = null;

        try
        {
            await WarmUpRepositoryRuntimeAsync();
            Console.Error.WriteLine(
                "Warmed SQLCipher and encrypted-repository runtime paths.");

            var databasePath = Path.Combine(workingDirectory, "metadata.db");
            using var keyProvider = new EphemeralKeyProvider();
            var metrics = new List<PerformanceMetric>();
            repository = new EncryptedSqliteMetadataRepository(
                databasePath,
                keyProvider);

            metrics.Add(await MeasureAsync(
                "encrypted_empty_initialize",
                "ms",
                EmptyInitializeLimitMilliseconds,
                async () => await repository.InitializeAsync(CancellationToken.None)));
            Console.Error.WriteLine("Initialized synthetic encrypted repository.");

            var clock = new FixedClock();
            var diagnosticSink = new NullDiagnosticSink();
            var syncStopwatch = Stopwatch.StartNew();
            for (var identityIndex = 0; identityIndex < IdentityCount; identityIndex++)
            {
                var identity = CreateIdentity(identityIndex);
                await repository.UpsertIdentityAsync(identity, CancellationToken.None);
                var provider = new SyntheticVaultProvider(identityIndex);
                var service = new SynchronizationService(
                    provider,
                    repository,
                    clock,
                    diagnosticSink);
                var run = await service.SynchronizeAsync(identity, CancellationToken.None);
                if (run.Status != SyncStatus.Completed ||
                    run.VaultCount != VaultsPerIdentity ||
                    run.ItemCount != VaultsPerIdentity * ItemsPerVault)
                {
                    throw new InvalidOperationException(
                        $"Synthetic sync {identityIndex} returned an unexpected result.");
                }

                Console.Error.WriteLine(FormattableString.Invariant(
                    $"Synchronized identity {identityIndex + 1}/{IdentityCount} in {syncStopwatch.Elapsed.TotalMilliseconds:F0} ms."));
            }

            syncStopwatch.Stop();
            metrics.Add(PerformanceMetric.UpperBound(
                "metadata_sync_50000_objects",
                syncStopwatch.Elapsed.TotalMilliseconds,
                "ms",
                MetadataSyncLimitMilliseconds));

            repository.Dispose();
            repository = new EncryptedSqliteMetadataRepository(
                databasePath,
                keyProvider);
            metrics.Add(await MeasureAsync(
                "encrypted_repository_reopen",
                "ms",
                RepositoryReopenLimitMilliseconds,
                async () => await repository.InitializeAsync(CancellationToken.None)));
            Console.Error.WriteLine("Reopened and validated synthetic encrypted repository.");

            var searchMetrics = await MeasureSearchAsync(repository, clock);
            metrics.AddRange(searchMetrics);
            Console.Error.WriteLine("Completed warm search measurements.");

            metrics.Add(await MeasureCancellationAsync(
                repository,
                clock,
                diagnosticSink));
            Console.Error.WriteLine("Completed synchronization cancellation measurement.");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var currentProcess = Process.GetCurrentProcess();
            currentProcess.Refresh();
            metrics.Add(PerformanceMetric.UpperBound(
                "private_memory_after_search",
                BytesToMebibytes(currentProcess.PrivateMemorySize64),
                "MiB",
                PrivateMemoryLimitMebibytes));
            metrics.Add(PerformanceMetric.UpperBound(
                "encrypted_database_size",
                BytesToMebibytes(new FileInfo(databasePath).Length),
                "MiB",
                DatabaseSizeLimitMebibytes));

            var report = new PerformanceReport(
                1,
                DateTimeOffset.UtcNow,
                options.Commit,
                new PerformanceEnvironment(
                    RuntimeInformation.OSDescription,
                    RuntimeInformation.FrameworkDescription,
                    RuntimeInformation.ProcessArchitecture.ToString(),
                    Environment.ProcessorCount),
                new EstateProfile(
                    IdentityCount,
                    IdentityCount * TenantsPerIdentity,
                    IdentityCount * SubscriptionsPerIdentity,
                    IdentityCount * VaultsPerIdentity,
                    IdentityCount * VaultsPerIdentity * ItemsPerVault,
                    SearchIterations),
                metrics,
                metrics.All(metric => metric.Passed),
                [
                    "Synthetic metadata contains no provider values, tokens, credentials, or live identifiers.",
                    "Empty repository initialization is measured after one isolated in-process SQLCipher and repository warmup; clean process-to-window startup remains a release-candidate live test.",
                    "Synchronization excludes provider network latency and measures the production service and encrypted repository.",
                    "Repository reopen is a core-startup measure; exact packaged UI startup remains a release-candidate live test.",
                    "Working set is measured after seeding, forced collection, warm searches, and cancellation in this probe process.",
                ]);

            var json = JsonSerializer.Serialize(
                report,
                PerformanceProbeJsonContext.Default.PerformanceReport);
            Console.WriteLine(json);
            if (options.OutputPath is not null)
            {
                var outputPath = Path.GetFullPath(options.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                await File.WriteAllTextAsync(
                    outputPath,
                    json + Environment.NewLine,
                    new UTF8Encoding(false));
            }

            return report.Passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Performance probe failed: {exception.GetType().Name}: {exception.Message}");
            return 2;
        }
        finally
        {
            repository?.Dispose();
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, true);
            }
        }
    }

    private static async Task WarmUpRepositoryRuntimeAsync()
    {
        var warmupDirectory = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-performance-warmup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(warmupDirectory);

        try
        {
            using var keyProvider = new EphemeralKeyProvider();
            using var repository = new EncryptedSqliteMetadataRepository(
                Path.Combine(warmupDirectory, "metadata.db"),
                keyProvider);
            await repository.InitializeAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(warmupDirectory))
            {
                Directory.Delete(warmupDirectory, true);
            }
        }
    }

    private static async Task<IReadOnlyList<PerformanceMetric>> MeasureSearchAsync(
        EncryptedSqliteMetadataRepository repository,
        IClock clock)
    {
        var searchService = new SearchService(repository, clock);
        SearchRequest[] requests =
        [
            new("secret-01234"),
            new("team-07"),
            new("production"),
            new(IdentityId: StableGuid("identity-3")),
            new(TenantId: StableGuid("tenant-4-1").ToString("D")),
            new(SubscriptionId: StableGuid("subscription-5-11").ToString("D")),
            new(VaultId: StableGuid("vault-6-13")),
            new(ObjectType: VaultObjectType.Secret),
            new(Enabled: true),
            new(Text: "secret", RecentlyAccessedFirst: true),
        ];

        foreach (var request in requests)
        {
            var warmup = await searchService.SearchAsync(request, CancellationToken.None);
            if (warmup.Count == 0)
            {
                throw new InvalidOperationException(
                    "A synthetic search probe returned no results.");
            }
        }

        var durations = new List<double>(SearchIterations);
        for (var iteration = 0; iteration < SearchIterations; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = await searchService.SearchAsync(
                requests[iteration % requests.Length],
                CancellationToken.None);
            stopwatch.Stop();
            if (results.Count == 0)
            {
                throw new InvalidOperationException(
                    "A measured synthetic search returned no results.");
            }

            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        durations.Sort();
        var percentileIndex = (int)Math.Ceiling(durations.Count * 0.95) - 1;
        return
        [
            PerformanceMetric.UpperBound(
                "search_p95",
                durations[percentileIndex],
                "ms",
                SearchP95LimitMilliseconds),
            PerformanceMetric.UpperBound(
                "search_maximum",
                durations[^1],
                "ms",
                SearchMaximumLimitMilliseconds),
        ];
    }

    private static async Task<PerformanceMetric> MeasureCancellationAsync(
        EncryptedSqliteMetadataRepository repository,
        IClock clock,
        IDiagnosticSink diagnosticSink)
    {
        var identity = CreateIdentity(IdentityCount);
        await repository.UpsertIdentityAsync(identity, CancellationToken.None);
        var provider = new CancellationProbeProvider();
        var service = new SynchronizationService(
            provider,
            repository,
            clock,
            diagnosticSink);
        using var cancellation = new CancellationTokenSource();
        var synchronization = service.SynchronizeAsync(identity, cancellation.Token);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var stopwatch = Stopwatch.StartNew();
        cancellation.Cancel();
        var run = await synchronization.WaitAsync(TimeSpan.FromSeconds(5));
        stopwatch.Stop();
        if (run.Status != SyncStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancellation probe did not return the cancelled sync status.");
        }

        return PerformanceMetric.UpperBound(
            "sync_cancellation_response",
            stopwatch.Elapsed.TotalMilliseconds,
            "ms",
            CancellationLimitMilliseconds);
    }

    private static async Task<PerformanceMetric> MeasureAsync(
        string name,
        string unit,
        double limit,
        Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return PerformanceMetric.UpperBound(
            name,
            stopwatch.Elapsed.TotalMilliseconds,
            unit,
            limit);
    }

    private static ConnectedIdentity CreateIdentity(int identityIndex) =>
        new(
            StableGuid($"identity-{identityIndex}"),
            StableGuid("client").ToString("D"),
            $"synthetic-account-{identityIndex:D2}",
            $"operator-{identityIndex:D2}@example.invalid",
            $"Synthetic identity {identityIndex:D2}",
            StableGuid($"tenant-{identityIndex}-0").ToString("D"),
            AuthenticationState.Ready,
            ObservedAt,
            true);

    private static DiscoverySnapshot CreateSnapshot(int identityIndex)
    {
        var identityId = StableGuid($"identity-{identityIndex}");
        var tenants = new List<TenantAccess>(TenantsPerIdentity);
        for (var tenantIndex = 0; tenantIndex < TenantsPerIdentity; tenantIndex++)
        {
            var tenantId = StableGuid($"tenant-{identityIndex}-{tenantIndex}");
            tenants.Add(new TenantAccess(
                StableGuid($"tenant-access-{identityIndex}-{tenantIndex}"),
                identityId,
                tenantId.ToString("D"),
                $"Synthetic tenant {identityIndex:D2}-{tenantIndex:D2}",
                tenantIndex == 0 ? "Home" : "Guest",
                ObservedAt,
                "Available"));
        }

        var subscriptions = new List<SubscriptionAccess>(
            SubscriptionsPerIdentity);
        var vaults = new List<VaultResource>(VaultsPerIdentity);
        var accessPaths = new List<VaultAccess>(VaultsPerIdentity);
        var items = new List<VaultItem>(VaultsPerIdentity * ItemsPerVault);

        for (var subscriptionIndex = 0;
             subscriptionIndex < SubscriptionsPerIdentity;
             subscriptionIndex++)
        {
            var tenantIndex = subscriptionIndex % TenantsPerIdentity;
            var tenantId = StableGuid($"tenant-{identityIndex}-{tenantIndex}");
            var subscriptionId = StableGuid(
                $"subscription-{identityIndex}-{subscriptionIndex}");
            subscriptions.Add(new SubscriptionAccess(
                StableGuid(
                    $"subscription-access-{identityIndex}-{subscriptionIndex}"),
                StableGuid($"tenant-access-{identityIndex}-{tenantIndex}"),
                subscriptionId.ToString("D"),
                $"Synthetic subscription {identityIndex:D2}-{subscriptionIndex:D2}",
                "Enabled",
                true,
                ObservedAt));

            var vaultId = StableGuid($"vault-{identityIndex}-{subscriptionIndex}");
            var vaultName =
                $"vp-synthetic-{identityIndex:D2}-{subscriptionIndex:D2}";
            vaults.Add(new VaultResource(
                vaultId,
                $"/subscriptions/{subscriptionId:D}/resourceGroups/rg-{identityIndex:D2}/providers/Microsoft.KeyVault/vaults/{vaultName}",
                vaultName,
                tenantId.ToString("D"),
                subscriptionId.ToString("D"),
                $"rg-{identityIndex:D2}",
                "eastus",
                new Dictionary<string, string>
                {
                    ["environment"] =
                        subscriptionIndex % 3 == 0 ? "production" : "test",
                },
                new Uri($"https://{vaultName}.vault.azure.net/"),
                ObservedAt));
            accessPaths.Add(new VaultAccess(
                StableGuid($"vault-access-{identityIndex}-{subscriptionIndex}"),
                vaultId,
                identityId,
                tenantId.ToString("D"),
                "Metadata list allowed; value read not tested",
                ObservedAt,
                null,
                0,
                true));

            for (var itemIndex = 0; itemIndex < ItemsPerVault; itemIndex++)
            {
                var identityItemIndex =
                    subscriptionIndex * ItemsPerVault + itemIndex;
                items.Add(new VaultItem(
                    StableGuid(
                        $"item-{identityIndex}-{subscriptionIndex}-{itemIndex}"),
                    vaultId,
                    $"secret-{identityItemIndex:D5}",
                    (VaultObjectType)(itemIndex % 3),
                    itemIndex % 10 != 0,
                    new Dictionary<string, string>
                    {
                        ["environment"] =
                            subscriptionIndex % 3 == 0 ? "production" : "test",
                        ["owner"] = $"team-{itemIndex % 25:D2}",
                    },
                    itemIndex % 2 == 0 ? "text/plain" : null,
                    ObservedAt.AddDays(-30),
                    ObservedAt.AddMinutes(-itemIndex),
                    itemIndex % 20 == 0
                        ? ObservedAt.AddDays(30)
                        : null,
                    "1",
                    $"synthetic-fingerprint-{identityIndex:D2}-{identityItemIndex:D5}",
                    ObservedAt));
            }
        }

        return new DiscoverySnapshot(
            tenants,
            subscriptions,
            vaults,
            accessPaths,
            items,
            []);
    }

    private static Guid StableGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static double BytesToMebibytes(long bytes) =>
        bytes / 1024d / 1024d;

    private sealed class SyntheticVaultProvider(int identityIndex)
        : IVaultProvider
    {
        public Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identity.Id != StableGuid($"identity-{identityIndex}") ||
                excludedSubscriptions.Count != 0 ||
                excludedVaultResourceIds.Count != 0)
            {
                throw new InvalidOperationException(
                    "Synthetic provider received an unexpected scope.");
            }

            return Task.FromResult(CreateSnapshot(identityIndex));
        }

        public Task<SensitiveValue> RetrieveSecretAsync(
            ConnectedIdentity identity,
            VaultResource vault,
            VaultItem item,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException(
                "The performance probe never retrieves protected values.");
    }

    private sealed class CancellationProbeProvider : IVaultProvider
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }

        public Task<SensitiveValue> RetrieveSecretAsync(
            ConnectedIdentity identity,
            VaultResource vault,
            VaultItem item,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException(
                "The cancellation probe never retrieves protected values.");
    }

    private sealed class EphemeralKeyProvider : IKeyMaterialProvider, IDisposable
    {
        private readonly Dictionary<string, byte[]> _keys = [];

        public bool IsAvailable => true;

        public Task<byte[]> GetOrCreateKeyAsync(
            string purpose,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_keys.TryGetValue(purpose, out var key))
            {
                key = RandomNumberGenerator.GetBytes(32);
                _keys.Add(purpose, key);
            }

            return Task.FromResult(key.ToArray());
        }

        public Task<byte[]> GetExistingKeyAsync(
            string purpose,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_keys.TryGetValue(purpose, out var key))
            {
                throw new ProtectedKeyUnavailableException(
                    "The synthetic metadata key is unavailable.");
            }

            return Task.FromResult(key.ToArray());
        }

        public void Dispose()
        {
            foreach (var key in _keys.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            _keys.Clear();
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => ObservedAt;
    }

    private sealed class NullDiagnosticSink : IDiagnosticSink
    {
        public void Information(
            string eventName,
            IReadOnlyDictionary<string, object?> fields)
        {
        }

        public void WriteError(
            string eventName,
            Exception exception,
            IReadOnlyDictionary<string, object?> fields)
        {
        }
    }

    private sealed record Options(string? OutputPath, string Commit)
    {
        public static Options Parse(string[] args)
        {
            string? outputPath = null;
            var commit = "uncommitted";
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--output" when index + 1 < args.Length:
                        outputPath = args[++index];
                        break;
                    case "--commit" when index + 1 < args.Length:
                        commit = args[++index];
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unknown or incomplete argument: {args[index]}");
                }
            }

            return new Options(outputPath, commit);
        }
    }
}

internal sealed record PerformanceMetric(
    string Name,
    double Actual,
    string Unit,
    double UpperLimit,
    bool Passed)
{
    public static PerformanceMetric UpperBound(
        string name,
        double actual,
        string unit,
        double upperLimit) =>
        new(
            name,
            Math.Round(actual, 3, MidpointRounding.AwayFromZero),
            unit,
            upperLimit,
            actual <= upperLimit);
}

internal sealed record PerformanceEnvironment(
    string OperatingSystem,
    string Framework,
    string Architecture,
    int LogicalProcessorCount);

internal sealed record EstateProfile(
    int Identities,
    int Tenants,
    int Subscriptions,
    int Vaults,
    int Objects,
    int SearchIterations);

internal sealed record PerformanceReport(
    int SchemaVersion,
    DateTimeOffset MeasuredAtUtc,
    string SourceCommit,
    PerformanceEnvironment Environment,
    EstateProfile Estate,
    IReadOnlyList<PerformanceMetric> Metrics,
    bool Passed,
    IReadOnlyList<string> Limitations);

[System.Text.Json.Serialization.JsonSerializable(typeof(PerformanceReport))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class PerformanceProbeJsonContext
    : System.Text.Json.Serialization.JsonSerializerContext;
