using System.Text.Json;
using System.Text.Json.Serialization;
using VaultProspector.BrowserProtocol;

namespace VaultProspector.BrowserHost;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserHostConfigurationDocument(
    int ProtocolVersion,
    string PipeName,
    string[] ChromiumExtensionIds,
    string[] FirefoxExtensionIds);

public sealed class BrowserHostConfiguration
{
    public const string FileName = "browser-host.json";
    public const string ChromiumManifestFileName =
        "com.hybridsolutionscloud.vaultprospector.chromium.json";
    public const string FirefoxManifestFileName =
        "com.hybridsolutionscloud.vaultprospector.firefox.json";

    private const int MaximumConfigurationBytes = 16 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = false,
            MaxDepth = 8,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

    private readonly HashSet<string> _chromiumExtensionIds;
    private readonly HashSet<string> _firefoxExtensionIds;

    private BrowserHostConfiguration(
        string pipeName,
        HashSet<string> chromiumExtensionIds,
        HashSet<string> firefoxExtensionIds)
    {
        PipeName = pipeName;
        _chromiumExtensionIds = chromiumExtensionIds;
        _firefoxExtensionIds = firefoxExtensionIds;
    }

    public string PipeName { get; }

    public static BrowserHostConfiguration Load(string applicationDirectory)
    {
        var directory = Path.GetFullPath(applicationDirectory);
        var path = Path.GetFullPath(Path.Combine(directory, FileName));
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(directory),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BrowserProtocolException("Browser host configuration path escaped the application directory.");
        }

        var file = new FileInfo(path);
        if (!file.Exists ||
            (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
            file.Length is < 2 or > MaximumConfigurationBytes)
        {
            throw new BrowserProtocolException("Browser host configuration is missing or invalid.");
        }

        var bytes = File.ReadAllBytes(path);
        try
        {
            BrowserMessageCodec.ValidateJsonObject(bytes);
            var document = JsonSerializer.Deserialize<BrowserHostConfigurationDocument>(
                bytes,
                SerializerOptions) ?? throw new BrowserProtocolException("Browser host configuration is empty.");
            return Create(document);
        }
        catch (JsonException exception)
        {
            throw new BrowserProtocolException(
                $"Browser host configuration does not match the schema: {exception.Message}");
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    public bool IsAllowed(BrowserFamily browserFamily, string extensionId) =>
        browserFamily switch
        {
            BrowserFamily.Chromium => _chromiumExtensionIds.Contains(extensionId),
            BrowserFamily.Firefox => _firefoxExtensionIds.Contains(extensionId),
            _ => false,
        };

    private static BrowserHostConfiguration Create(BrowserHostConfigurationDocument document)
    {
        if (document.ProtocolVersion != BrowserProtocolConstants.CurrentVersion ||
            string.IsNullOrWhiteSpace(document.PipeName) ||
            document.PipeName.Length > 128 ||
            document.PipeName.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and
                not '-'))
        {
            throw new BrowserProtocolException("Browser host configuration envelope is invalid.");
        }

        var chromium = ValidateIds(BrowserFamily.Chromium, document.ChromiumExtensionIds);
        var firefox = ValidateIds(BrowserFamily.Firefox, document.FirefoxExtensionIds);
        if (chromium.Count == 0 && firefox.Count == 0)
            throw new BrowserProtocolException("Browser host configuration has no allowed extension identity.");

        return new BrowserHostConfiguration(document.PipeName, chromium, firefox);
    }

    private static HashSet<string> ValidateIds(
        BrowserFamily family,
        string[]? extensionIds)
    {
        if (extensionIds is null || extensionIds.Length > 8)
            throw new BrowserProtocolException("Browser host extension allowlist is invalid.");

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var extensionId in extensionIds)
        {
            BrowserBrokerAuthentication.ValidateExtensionId(family, extensionId);
            if (!result.Add(extensionId))
                throw new BrowserProtocolException("Browser host extension allowlist contains a duplicate.");
        }

        return result;
    }
}
