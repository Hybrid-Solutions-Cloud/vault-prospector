using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Mobile.Autofill;

public sealed record MobileAutofillRequest(
    Uri Origin,
    BrowserMappingFieldPurpose FieldPurpose,
    VaultObjectType ObjectType,
    bool HasExactMapping,
    bool IsForegroundInvocation,
    bool HasFreshUserVerification);

public static class MobileAutofillPolicy
{
    public static bool CanOffer(MobileAutofillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return IsSupportedOrigin(request.Origin)
            && request.ObjectType == VaultObjectType.Secret
            && request.HasExactMapping
            && request.IsForegroundInvocation
            && request.HasFreshUserVerification
            && request.FieldPurpose is
                BrowserMappingFieldPurpose.Username or
                BrowserMappingFieldPurpose.Password;
    }

    private static bool IsSupportedOrigin(Uri origin)
    {
        if (origin is null || !origin.IsAbsoluteUri)
            return false;

        try
        {
            var canonical = CanonicalBrowserOrigin.Parse(origin.AbsoluteUri);
            return canonical.Port == 443;
        }
        catch (BrowserProtocolException)
        {
            return false;
        }
    }
}
