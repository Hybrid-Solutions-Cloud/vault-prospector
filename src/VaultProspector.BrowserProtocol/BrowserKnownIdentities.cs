namespace VaultProspector.BrowserProtocol;

public static class BrowserKnownIdentities
{
    public const string ChromiumDevelopment =
        "fmkdaepdbgdbhdhcednhppbhhejeabin";

    public const string Firefox =
        "vault-prospector@hybrid-solutions.cloud";

    public static bool IsAllowed(BrowserFamily browserFamily, string extensionId) =>
        browserFamily switch
        {
            BrowserFamily.Chromium =>
                string.Equals(
                    extensionId,
                    ChromiumDevelopment,
                    StringComparison.Ordinal),
            BrowserFamily.Firefox =>
                string.Equals(
                    extensionId,
                    Firefox,
                    StringComparison.Ordinal),
            _ => false,
        };
}
