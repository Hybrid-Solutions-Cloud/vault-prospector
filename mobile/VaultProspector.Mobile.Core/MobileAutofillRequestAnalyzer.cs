using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Mobile.Core;

public sealed record MobileAutofillFieldDescriptor(
    string Id,
    IReadOnlyList<string> Hints);

public sealed record MobileAutofillNativeRequest(
    string Scheme,
    string Domain,
    IReadOnlyList<MobileAutofillFieldDescriptor> Fields);

public sealed record MobileAutofillField(
    string Id,
    BrowserMappingFieldPurpose Purpose);

public sealed record MobileAutofillCandidate(
    Uri Origin,
    IReadOnlyList<MobileAutofillField> Fields);

public static class MobileAutofillRequestAnalyzer
{
    private const string UsernameHint = "username";
    private const string PasswordHint = "password";

    public static bool TryAnalyze(
        MobileAutofillNativeRequest request,
        out MobileAutofillCandidate? candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        candidate = null;

        if (!TryCreateOrigin(request.Scheme, request.Domain, out var origin) ||
            request.Fields is null ||
            request.Fields.Count is 0)
        {
            return false;
        }

        var supportedFields = new List<MobileAutofillField>(2);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var purposes = new HashSet<BrowserMappingFieldPurpose>();

        foreach (var descriptor in request.Fields)
        {
            if (descriptor is null ||
                string.IsNullOrWhiteSpace(descriptor.Id) ||
                descriptor.Id.Length > 256 ||
                descriptor.Hints is null)
            {
                return false;
            }

            var purpose = GetPurpose(descriptor.Hints);
            if (purpose is null)
                continue;

            if (!ids.Add(descriptor.Id) || !purposes.Add(purpose.Value))
                return false;

            supportedFields.Add(new MobileAutofillField(
                descriptor.Id,
                purpose.Value));
        }

        if (supportedFields.Count is 0)
            return false;

        candidate = new MobileAutofillCandidate(origin!, supportedFields);
        return true;
    }

    public static bool TryCreateOrigin(
        string scheme,
        string domain,
        out Uri? origin)
    {
        origin = null;
        if (!string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(domain) ||
            domain.Length > 255 ||
            domain.Any(char.IsWhiteSpace) ||
            domain.Contains('/') ||
            domain.Contains('\\') ||
            domain.Contains('@') ||
            domain.Contains(':'))
        {
            return false;
        }

        try
        {
            var canonical = CanonicalBrowserOrigin.Parse(
                $"{Uri.UriSchemeHttps}://{domain}");
            if (canonical.Port != 443)
                return false;

            origin = new Uri(canonical.SerializedOrigin, UriKind.Absolute);
            return true;
        }
        catch (BrowserProtocolException)
        {
            return false;
        }
    }

    public static bool TryCreateAppleServiceOrigin(
        string serviceIdentifier,
        bool isDomainIdentifier,
        out Uri? origin)
    {
        origin = null;
        if (string.IsNullOrWhiteSpace(serviceIdentifier))
            return false;

        if (isDomainIdentifier)
        {
            return TryCreateOrigin(
                Uri.UriSchemeHttps,
                serviceIdentifier,
                out origin);
        }

        try
        {
            var canonical = CanonicalBrowserOrigin.Parse(serviceIdentifier);
            if (canonical.Port != 443)
                return false;

            origin = new Uri(canonical.SerializedOrigin, UriKind.Absolute);
            return true;
        }
        catch (BrowserProtocolException)
        {
            return false;
        }
    }

    private static BrowserMappingFieldPurpose? GetPurpose(
        IReadOnlyList<string> hints)
    {
        var username = false;
        var password = false;
        foreach (var hint in hints)
        {
            if (string.Equals(
                    hint,
                    UsernameHint,
                    StringComparison.OrdinalIgnoreCase))
            {
                username = true;
            }
            else if (string.Equals(
                         hint,
                         PasswordHint,
                         StringComparison.OrdinalIgnoreCase))
            {
                password = true;
            }
        }

        if (username == password)
            return null;

        return username
            ? BrowserMappingFieldPurpose.Username
            : BrowserMappingFieldPurpose.Password;
    }
}
