using VaultProspector.Application;

namespace VaultProspector.Providers.Azure;

public sealed class ManagedIdentityEnvironmentDetector : IManagedIdentityEnvironmentDetector
{
    private static readonly Uri InstanceMetadataUri =
        new("http://169.254.169.254/metadata/instance?api-version=2021-02-01");
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _environmentReader;

    public ManagedIdentityEnvironmentDetector(HttpClient httpClient)
        : this(httpClient, Environment.GetEnvironmentVariable)
    {
    }

    public ManagedIdentityEnvironmentDetector(
        HttpClient httpClient,
        Func<string, string?> environmentReader)
    {
        _httpClient = httpClient;
        _environmentReader = environmentReader;
    }

    public async Task<ManagedIdentityEnvironmentStatus> DetectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasHostManagedIdentityEndpoint())
        {
            return new ManagedIdentityEnvironmentStatus(
                true,
                "This Azure host exposes a managed-identity endpoint. Availability still depends on an assigned identity and Azure authorization.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, InstanceMetadataUri);
            request.Headers.Add("Metadata", "true");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return response.IsSuccessStatusCode
                ? new ManagedIdentityEnvironmentStatus(
                    true,
                    "Azure Instance Metadata Service is available. Availability still depends on an assigned identity and Azure authorization.")
                : Unsupported();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unsupported();
        }
        catch (HttpRequestException)
        {
            return Unsupported();
        }
    }

    private bool HasHostManagedIdentityEndpoint() =>
        HasValue("IDENTITY_ENDPOINT") ||
        HasValue("MSI_ENDPOINT") ||
        HasValue("IMDS_ENDPOINT");

    private bool HasValue(string name) =>
        !string.IsNullOrWhiteSpace(_environmentReader(name));

    private static ManagedIdentityEnvironmentStatus Unsupported() =>
        new(
            false,
            "Managed identity is unavailable on this host. Use interactive Microsoft Entra sign-in or a certificate-based service principal.");
}
