using VaultProspector.BrowserProtocol;
using VaultProspector.Domain;

namespace VaultProspector.Mobile.Core;

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

        return request.Origin.IsAbsoluteUri
            && string.Equals(request.Origin.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && string.IsNullOrEmpty(request.Origin.UserInfo)
            && !string.IsNullOrWhiteSpace(request.Origin.IdnHost)
            && request.Origin.IsDefaultPort
            && request.ObjectType == VaultObjectType.Secret
            && request.HasExactMapping
            && request.IsForegroundInvocation
            && request.HasFreshUserVerification
            && request.FieldPurpose is
                BrowserMappingFieldPurpose.Username or
                BrowserMappingFieldPurpose.Password;
    }
}
