using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using VaultProspector.Application;
using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Platform;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserFillPolicyDocument(
    int Version,
    bool Enabled,
    BrowserFillPolicyDestination[] AllowedDestinations);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BrowserFillPolicyDestination(
    string TopOrigin,
    string FrameOrigin,
    BrowserFamily[] BrowserFamilies,
    BrowserMappingFieldPurpose[] FieldPurposes);

public sealed class WindowsMachineBrowserFillPolicy(
    string policyPath,
    string trustedInstallationRoot) : IBrowserFillPolicy
{
    private const int MaximumPolicyBytes = 64 * 1024;
    private const int MaximumDestinations = 128;

    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    private readonly string _policyPath = Path.GetFullPath(policyPath);
    private readonly string _trustedInstallationRoot =
        Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(trustedInstallationRoot));

    public async Task<BrowserFillPolicyDecision> EvaluateAsync(
        BrowserFamily browserFamily,
        CanonicalBrowserOrigin topOrigin,
        CanonicalBrowserOrigin frameOrigin,
        BrowserMappingFieldPurpose fieldPurpose,
        CancellationToken cancellationToken)
    {
        var load = await LoadAsync(cancellationToken);
        if (load.Document is null)
            return new BrowserFillPolicyDecision(false, load.SafeStatus);

        var allowed = load.Document.AllowedDestinations.Any(destination =>
            destination.BrowserFamilies.Contains(browserFamily) &&
            destination.FieldPurposes.Contains(fieldPurpose) &&
            string.Equals(
                destination.TopOrigin,
                topOrigin.SerializedOrigin,
                StringComparison.Ordinal) &&
            string.Equals(
                destination.FrameOrigin,
                frameOrigin.SerializedOrigin,
                StringComparison.Ordinal));
        return allowed
            ? new BrowserFillPolicyDecision(
                true,
                "Machine policy allows this exact browser destination and field purpose.")
            : new BrowserFillPolicyDecision(
                false,
                "Machine policy does not allow this exact browser destination and field purpose.");
    }

    public async Task<string> GetStatusAsync(CancellationToken cancellationToken)
    {
        var load = await LoadAsync(cancellationToken);
        return load.Document is null
            ? load.SafeStatus
            : $"Machine browser-fill policy is enabled for {load.Document.AllowedDestinations.Length} exact destination rule(s).";
    }

    private async Task<PolicyLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return new PolicyLoadResult(
                null,
                "Browser fill is disabled because Windows machine policy is unavailable.");

        var trustedPrefix =
            $"{_trustedInstallationRoot}{Path.DirectorySeparatorChar}";
        if (string.IsNullOrWhiteSpace(_trustedInstallationRoot) ||
            !_policyPath.StartsWith(
                trustedPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return new PolicyLoadResult(
                null,
                "Browser fill is disabled because its policy is outside the protected installation directory.");
        }

        var file = new FileInfo(_policyPath);
        file.Refresh();
        if (!file.Exists)
            return new PolicyLoadResult(
                null,
                "Browser fill is disabled because machine policy is not installed.");
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0 ||
            file.Length is < 2 or > MaximumPolicyBytes)
        {
            return new PolicyLoadResult(
                null,
                "Browser fill is disabled because machine policy failed validation.");
        }

        byte[]? bytes = null;
        try
        {
            bytes = await File.ReadAllBytesAsync(_policyPath, cancellationToken);
            BrowserMessageCodec.ValidateJsonObject(bytes);
            var document = JsonSerializer.Deserialize<BrowserFillPolicyDocument>(
                bytes,
                SerializerOptions);
            if (document is null ||
                document.Version != BrowserProtocolConstants.CurrentVersion ||
                !document.Enabled ||
                document.AllowedDestinations is null or
                { Length: 0 } or
                { Length: > MaximumDestinations })
            {
                return new PolicyLoadResult(
                    null,
                    "Browser fill is disabled by machine policy.");
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var destination in document.AllowedDestinations)
            {
                if (destination.BrowserFamilies is null or { Length: 0 } ||
                    destination.BrowserFamilies.Any(family => !Enum.IsDefined(family)) ||
                    destination.FieldPurposes is null or { Length: 0 } ||
                    destination.FieldPurposes.Any(purpose => !Enum.IsDefined(purpose)))
                {
                    return InvalidPolicy();
                }

                var top = CanonicalBrowserOrigin.Parse(destination.TopOrigin);
                var frame = CanonicalBrowserOrigin.Parse(destination.FrameOrigin);
                if (!string.Equals(
                        top.SerializedOrigin,
                        destination.TopOrigin,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        frame.SerializedOrigin,
                        destination.FrameOrigin,
                        StringComparison.Ordinal))
                {
                    return InvalidPolicy();
                }

                foreach (var family in destination.BrowserFamilies.Distinct())
                {
                    foreach (var purpose in destination.FieldPurposes.Distinct())
                    {
                        if (!unique.Add(
                                $"{(int)family}|{(int)purpose}|{top.SerializedOrigin}|{frame.SerializedOrigin}"))
                        {
                            return InvalidPolicy();
                        }
                    }
                }
            }

            return new PolicyLoadResult(document, string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            BrowserProtocolException)
        {
            return InvalidPolicy();
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static PolicyLoadResult InvalidPolicy() =>
        new(
            null,
            "Browser fill is disabled because machine policy failed validation.");

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = false,
            MaxDepth = 12,
            NumberHandling = JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }

    private sealed record PolicyLoadResult(
        BrowserFillPolicyDocument? Document,
        string SafeStatus);
}
