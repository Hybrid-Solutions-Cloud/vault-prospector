using System.Text.Json;

namespace VaultProspector.App;

public sealed record AppSettings(
    string ClientId,
    int ClipboardClearSeconds = 30,
    bool OfflineCacheEnabled = false,
    int MaximumCacheHours = 8)
{
    public static AppSettings Default { get; } = new(string.Empty);
}

public sealed class AppSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return AppSettings.Default;
        return JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path, cancellationToken), Options) ?? AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(settings, Options), cancellationToken);
    }
}
