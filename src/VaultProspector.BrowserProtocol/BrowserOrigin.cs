using System.Globalization;

namespace VaultProspector.BrowserProtocol;

public sealed record CanonicalBrowserOrigin(
    string SerializedOrigin,
    string AsciiHost,
    string UnicodeHost,
    int Port)
{
    public static CanonicalBrowserOrigin Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2_048)
            throw new BrowserProtocolException("Origin is missing or exceeds the protocol limit.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new BrowserProtocolException("Origin is not an absolute URI.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new BrowserProtocolException("Only HTTPS origins are supported.");

        if (!string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.AbsolutePath.Length > 0 && !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)))
        {
            throw new BrowserProtocolException("Origin must not contain credentials, path, query, or fragment.");
        }

        if (uri.HostNameType is not UriHostNameType.Dns ||
            string.IsNullOrWhiteSpace(uri.IdnHost) ||
            uri.IdnHost[^1] == '.')
        {
            throw new BrowserProtocolException("Origin must use a canonical DNS host.");
        }

        var asciiHost = uri.IdnHost.ToLowerInvariant();
        ValidateAsciiHost(asciiHost);

        var port = uri.IsDefaultPort ? 443 : uri.Port;
        if (port is < 1 or > 65_535)
            throw new BrowserProtocolException("Origin port is outside the supported range.");

        string unicodeHost;
        try
        {
            unicodeHost = new IdnMapping().GetUnicode(asciiHost);
        }
        catch (ArgumentException exception)
        {
            throw new BrowserProtocolException($"Origin contains an invalid internationalized host: {exception.Message}");
        }

        var serialized = port == 443
            ? $"https://{asciiHost}"
            : $"https://{asciiHost}:{port.ToString(CultureInfo.InvariantCulture)}";

        return new CanonicalBrowserOrigin(serialized, asciiHost, unicodeHost, port);
    }

    private static void ValidateAsciiHost(string host)
    {
        if (host.Length > 255)
            throw new BrowserProtocolException("Origin host exceeds the DNS length limit.");

        var labels = host.Split('.');
        if (labels.Length < 2)
            throw new BrowserProtocolException("Origin host must be a fully qualified DNS name.");

        foreach (var label in labels)
        {
            if (label.Length is < 1 or > 63 ||
                label[0] == '-' ||
                label[^1] == '-' ||
                label.Any(character =>
                    character is not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and
                    not '-'))
            {
                throw new BrowserProtocolException("Origin contains an invalid DNS label.");
            }
        }
    }
}
