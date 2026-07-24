using AuthenticationServices;
using Foundation;
using VaultProspector.Mobile.Autofill;

namespace VaultProspector.Mobile.iOS.CredentialProvider;

[Register("CredentialProviderViewController")]
#pragma warning disable CA1010
public sealed class CredentialProviderViewController
    : ASCredentialProviderViewController
#pragma warning restore CA1010
{
    public override void ProvideCredentialWithoutUserInteraction(
        ASPasswordCredentialIdentity credentialIdentity)
    {
        ArgumentNullException.ThrowIfNull(credentialIdentity);

        Cancel(IsSafeCredentialIdentity(credentialIdentity)
            ? ASExtensionErrorCode.UserInteractionRequired
            : ASExtensionErrorCode.CredentialIdentityNotFound);
    }

    public override void PrepareInterfaceToProvideCredential(
        ASPasswordCredentialIdentity credentialIdentity)
    {
        ArgumentNullException.ThrowIfNull(credentialIdentity);

        // The extension intentionally cannot read the containing app's Keychain or database.
        // A future reviewed implementation must authenticate in this visible UI, resolve one
        // opaque record identifier to one encrypted exact-origin mapping, retrieve one value,
        // and complete the request without persisting the value in the extension.
        Cancel(ASExtensionErrorCode.CredentialIdentityNotFound);
    }

    public override void PrepareCredentialList(
        ASCredentialServiceIdentifier[] serviceIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(serviceIdentifiers);
        Cancel(ASExtensionErrorCode.CredentialIdentityNotFound);
    }

    private static bool IsSafeCredentialIdentity(
        ASPasswordCredentialIdentity identity)
    {
        var service = identity.ServiceIdentifier;
        return service is not null &&
            MobileAutofillRequestAnalyzer.TryCreateAppleServiceOrigin(
                service.Identifier,
                service.Type ==
                    ASCredentialServiceIdentifierType.Domain,
                out _);
    }

    private void Cancel(ASExtensionErrorCode errorCode)
    {
        var domain = new NSString(
            errorCode.GetDomain() ?? "ASExtensionErrorDomain");
        var error = new NSError(domain, (nint)errorCode);
        ExtensionContext.CancelRequest(error);
    }
}
