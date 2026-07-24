using System.Text.Json;

namespace VaultProspector.App;

public enum CloseBehavior
{
    Ask,
    Exit,
    LockToNotificationArea,
}

public sealed record AppSettings(
    string ClientId,
    int ClipboardClearSeconds = 30,
    bool OfflineCacheEnabled = false,
    int MaximumCacheHours = 8,
    bool UseCustomClientId = false,
    CloseBehavior CloseBehavior = CloseBehavior.Ask,
    bool BackgroundMetadataSyncEnabled = false)
{
    public static AppSettings Default { get; } = new(ProductIdentity.DefaultClientId);
}

public sealed class AppSettingsStore(string path)
{
    private const int MaximumSettingsBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return AppSettings.Default;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumSettingsBytes)
            throw new JsonException("The settings file exceeds the safe size limit.");
        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            Options,
            cancellationToken) ?? AppSettings.Default;
        var normalizedClientId = loaded.ClientId?.Trim() ?? string.Empty;
        var useCustomClientId = loaded.UseCustomClientId ||
            (!string.IsNullOrWhiteSpace(normalizedClientId) &&
             !string.Equals(normalizedClientId, ProductIdentity.DefaultClientId, StringComparison.OrdinalIgnoreCase));
        return loaded with
        {
            ClientId = useCustomClientId ? normalizedClientId : ProductIdentity.DefaultClientId,
            UseCustomClientId = useCustomClientId,
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings, Options), cancellationToken);
    }
}
