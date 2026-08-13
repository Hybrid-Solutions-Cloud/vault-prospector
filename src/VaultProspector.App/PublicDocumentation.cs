using System.Diagnostics;

namespace VaultProspector.App;

public static class PublicDocumentation
{
    private const string DocumentationRoot =
        "https://hybrid-solutions-cloud.github.io/vault-prospector/";

    public static Uri UserGuide { get; } = new($"{DocumentationRoot}user-guide");
    public static Uri Roadmap { get; } = new($"{DocumentationRoot}product/roadmap");
    public static Uri Changelog { get; } = new($"{DocumentationRoot}changelog");
    public static Uri ReleaseGuide { get; } = new($"{DocumentationRoot}release");
    public static Uri ReleaseHistory { get; } = new(
        "https://github.com/Hybrid-Solutions-Cloud/vault-prospector-releases/releases");

    public static IReadOnlyList<Uri> All { get; } =
        [UserGuide, Roadmap, Changelog, ReleaseGuide, ReleaseHistory];

    public static bool IsCanonical(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        ((uri.Host.Equals(
              "hybrid-solutions-cloud.github.io",
              StringComparison.OrdinalIgnoreCase) &&
          uri.AbsolutePath.StartsWith(
              "/vault-prospector/",
              StringComparison.Ordinal)) ||
         (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
          uri.AbsolutePath.StartsWith(
              "/Hybrid-Solutions-Cloud/vault-prospector-releases/releases",
              StringComparison.Ordinal)));
}

public interface IExternalUriLauncher
{
    bool TryOpen(Uri uri);
}

public sealed class SystemExternalUriLauncher : IExternalUriLauncher
{
    public static SystemExternalUriLauncher Instance { get; } = new();

    public bool TryOpen(Uri uri)
    {
        if (!PublicDocumentation.IsCanonical(uri))
            return false;

        try
        {
            return Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true,
            }) is not null;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or
            InvalidOperationException)
        {
            return false;
        }
    }
}
