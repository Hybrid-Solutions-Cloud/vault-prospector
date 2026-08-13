using System.Text.Json;

namespace VaultProspector.Application;

public enum ReleaseUpdateAvailability
{
    Current,
    Available,
    DevelopmentBuild,
}

public sealed record ReleaseUpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    ReleaseUpdateAvailability Availability,
    string ReleaseName,
    string ReleaseNotes,
    Uri ReleasePageUri,
    Uri PackageUri,
    Uri ChecksumUri,
    string PackageName,
    long PackageSize,
    string ExpectedSha256,
    DateTimeOffset PublishedAt);

public interface IReleaseUpdateService
{
    Task<ReleaseUpdateInfo> CheckAsync(CancellationToken cancellationToken);
}

public sealed class GitHubReleaseUpdateService(
    HttpClient httpClient,
    string currentVersion) : IReleaseUpdateService
{
    private const string ExpectedPublisher = "hcs-platform-app[bot]";
    private const string ReleaseRepository =
        "Hybrid-Solutions-Cloud/vault-prospector-releases";
    private static readonly Uri ReleasesApiUri = new(
        $"https://api.github.com/repos/{ReleaseRepository}/releases?per_page=20");
    private static readonly string ReleaseDownloadPrefix =
        $"https://github.com/{ReleaseRepository}/releases/download/";
    private static readonly string ReleasePagePrefix =
        $"https://github.com/{ReleaseRepository}/releases/tag/";
    public async Task<ReleaseUpdateInfo> CheckAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            ReleasesApiUri);
        request.Headers.Accept.ParseAdd(
            "application/vnd.github+json");
        request.Headers.Add(
            "X-GitHub-Api-Version",
            "2022-11-28");
        request.Headers.UserAgent.ParseAdd(
            "VaultProspector-UpdateClient/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 2_097_152)
        {
            throw new InvalidDataException(
                "Release metadata exceeded the supported size.");
        }

        await using var content = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            content,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            },
            cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Release metadata did not contain a release list.");
        }

        ReleaseCandidate? selected = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            var candidate = TryReadCandidate(release);
            if (candidate is null ||
                selected is not null &&
                candidate.Version.CompareTo(selected.Version) <= 0)
            {
                continue;
            }

            selected = candidate;
        }

        if (selected is null)
        {
            throw new InvalidDataException(
                "No trusted supported Vault Prospector release was found.");
        }

        var parsedCurrent = ProductVersion.TryParse(
            NormalizeVersion(currentVersion));
        var availability = parsedCurrent is null
            ? ReleaseUpdateAvailability.DevelopmentBuild
            : selected.Version.CompareTo(parsedCurrent.Value) > 0
                ? ReleaseUpdateAvailability.Available
                : ReleaseUpdateAvailability.Current;
        return new ReleaseUpdateInfo(
            currentVersion,
            selected.Version.ToString(),
            availability,
            selected.ReleaseName,
            selected.ReleaseNotes,
            selected.ReleasePageUri,
            selected.PackageUri,
            selected.ChecksumUri,
            selected.PackageName,
            selected.PackageSize,
            selected.ExpectedSha256,
            selected.PublishedAt);
    }

    private static ReleaseCandidate? TryReadCandidate(
        JsonElement release)
    {
        try
        {
            if (release.GetProperty("draft").GetBoolean())
                return null;
            var publisher = release
                .GetProperty("author")
                .GetProperty("login")
                .GetString();
            if (!string.Equals(
                    publisher,
                    ExpectedPublisher,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var tag = release.GetProperty("tag_name").GetString();
            var normalizedVersion = NormalizeVersion(tag);
            var version = ProductVersion.TryParse(
                normalizedVersion);
            if (version is null)
                return null;
            var releaseName =
                release.GetProperty("name").GetString() ??
                $"Vault Prospector {normalizedVersion}";
            var releaseNotes =
                release.GetProperty("body").GetString() ??
                "No release notes were provided.";
            if (releaseName.Contains(
                    "withdrawn",
                    StringComparison.OrdinalIgnoreCase) ||
                releaseNotes.Contains(
                    "withdrawn",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var packageName =
                $"VaultProspector-{normalizedVersion}-win-x64.msi";
            var checksumName =
                $"{packageName}.sha256";
            var sigstoreName =
                $"{packageName}.sigstore.json";
            JsonElement? package = null;
            JsonElement? checksum = null;
            var hasSigstoreBundle = false;
            foreach (var asset in release
                         .GetProperty("assets")
                         .EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (string.Equals(
                        name,
                        packageName,
                        StringComparison.Ordinal))
                {
                    package = asset;
                }
                else if (string.Equals(
                             name,
                             checksumName,
                             StringComparison.Ordinal))
                {
                    checksum = asset;
                }
                else if (string.Equals(
                             name,
                             sigstoreName,
                             StringComparison.Ordinal))
                {
                    hasSigstoreBundle = true;
                }
            }

            if (package is null ||
                checksum is null ||
                !hasSigstoreBundle)
            {
                return null;
            }

            var packageUri = ReadTrustedDownloadUri(
                package.Value);
            var checksumUri = ReadTrustedDownloadUri(
                checksum.Value);
            var releasePageUri = new Uri(
                release.GetProperty("html_url").GetString() ??
                string.Empty,
                UriKind.Absolute);
            if (!releasePageUri.AbsoluteUri.StartsWith(
                    ReleasePagePrefix,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var digest = package.Value
                .GetProperty("digest")
                .GetString();
            if (digest is null ||
                !digest.StartsWith(
                    "sha256:",
                    StringComparison.Ordinal) ||
                !IsSha256(digest[7..]))
            {
                return null;
            }

            var packageSize =
                package.Value.GetProperty("size").GetInt64();
            if (packageSize is < 1 or > 536_870_912)
                return null;
            var checksumSize =
                checksum.Value.GetProperty("size").GetInt64();
            if (checksumSize is < 1 or > 4096)
                return null;

            return new ReleaseCandidate(
                version.Value,
                releaseName,
                releaseNotes.Length <= 8_000
                    ? releaseNotes
                    : releaseNotes[..8_000],
                releasePageUri,
                packageUri,
                checksumUri,
                packageName,
                packageSize,
                digest[7..].ToUpperInvariant(),
                release
                    .GetProperty("published_at")
                    .GetDateTimeOffset());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                FormatException or
                KeyNotFoundException or
                UriFormatException)
        {
            return null;
        }
    }

    private static Uri ReadTrustedDownloadUri(
        JsonElement asset)
    {
        var uri = new Uri(
            asset.GetProperty("browser_download_url").GetString() ??
            string.Empty,
            UriKind.Absolute);
        ValidateDownloadUri(uri);
        return uri;
    }

    private static void ValidateDownloadUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.AbsoluteUri.StartsWith(
                ReleaseDownloadPrefix,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A release asset URI was outside the trusted repository.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(character =>
            character is >= '0' and <= '9' or
                >= 'a' and <= 'f' or
                >= 'A' and <= 'F');

    private static string NormalizeVersion(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.StartsWith(
                'v') ||
            normalized.StartsWith(
                'V'))
        {
            normalized = normalized[1..];
        }

        var buildIndex = normalized.IndexOf(
            '+',
            StringComparison.Ordinal);
        return buildIndex >= 0
            ? normalized[..buildIndex]
            : normalized;
    }

    private sealed record ReleaseCandidate(
        ProductVersion Version,
        string ReleaseName,
        string ReleaseNotes,
        Uri ReleasePageUri,
        Uri PackageUri,
        Uri ChecksumUri,
        string PackageName,
        long PackageSize,
        string ExpectedSha256,
        DateTimeOffset PublishedAt);

    private readonly record struct ProductVersion(
        int Major,
        int Minor,
        int Patch,
        string? Prerelease) : IComparable<ProductVersion>
    {
        public static ProductVersion? TryParse(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 64)
            {
                return null;
            }

            var separator = value.IndexOf(
                '-',
                StringComparison.Ordinal);
            var numeric = separator >= 0
                ? value[..separator]
                : value;
            var prerelease = separator >= 0
                ? value[(separator + 1)..]
                : null;
            var segments = numeric.Split('.');
            if (segments.Length != 3 ||
                !int.TryParse(
                    segments[0],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var major) ||
                !int.TryParse(
                    segments[1],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minor) ||
                !int.TryParse(
                    segments[2],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var patch) ||
                prerelease is not null &&
                (prerelease.Length == 0 ||
                 prerelease.Any(character =>
                     !char.IsAsciiLetterOrDigit(character) &&
                     character is not '.' and not '-')))
            {
                return null;
            }

            return new ProductVersion(
                major,
                minor,
                patch,
                prerelease);
        }

        public int CompareTo(
            ProductVersion other)
        {
            var numeric = Major.CompareTo(other.Major);
            if (numeric != 0)
                return numeric;
            numeric = Minor.CompareTo(other.Minor);
            if (numeric != 0)
                return numeric;
            numeric = Patch.CompareTo(other.Patch);
            if (numeric != 0)
                return numeric;
            if (Prerelease is null)
                return other.Prerelease is null ? 0 : 1;
            if (other.Prerelease is null)
                return -1;

            var left = Prerelease.Split('.');
            var right = other.Prerelease.Split('.');
            for (var index = 0;
                 index < Math.Max(left.Length, right.Length);
                 index++)
            {
                if (index >= left.Length)
                    return -1;
                if (index >= right.Length)
                    return 1;
                var leftNumeric = int.TryParse(
                    left[index],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var leftValue);
                var rightNumeric = int.TryParse(
                    right[index],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var rightValue);
                int comparison;
                if (leftNumeric && rightNumeric)
                {
                    comparison = leftValue.CompareTo(
                        rightValue);
                }
                else if (leftNumeric)
                {
                    comparison = -1;
                }
                else if (rightNumeric)
                {
                    comparison = 1;
                }
                else
                {
                    comparison = string.Compare(
                        left[index],
                        right[index],
                        StringComparison.Ordinal);
                }

                if (comparison != 0)
                    return comparison;
            }

            return 0;
        }

        public override string ToString() =>
            Prerelease is null
                ? $"{Major}.{Minor}.{Patch}"
                : $"{Major}.{Minor}.{Patch}-{Prerelease}";
    }
}
