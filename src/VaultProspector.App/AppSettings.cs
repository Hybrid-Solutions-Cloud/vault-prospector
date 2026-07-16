using System.Text.Json;

namespace VaultProspector.App;

public sealed record AppSettings(
    string ClientId,
    int ClipboardClearSeconds = 30,
    bool OfflineCacheEnabled = false,
    int MaximumCacheHours = 8,
    bool UseCustomClientId = false)
{
    public static AppSettings Default { get; } = new(ProductIdentity.DefaultClientId);
}

public sealed class AppSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return AppSettings.Default;
        var loaded = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path, cancellationToken), Options) ?? AppSettings.Default;
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
