using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Core;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.Azure;

public sealed class AzureGovernedMutationProvider(
    IAzureCredentialProvider credentialProvider,
    HttpClient keyVaultClient,
    HttpClient authorizationClient) : IGovernedAzureMutationProvider
{
    private const string ApiVersion = "7.5";
    private readonly AzureAuthorizationEvidenceEvaluator
        _authorizationEvaluator = new(authorizationClient);

    public async Task<string> GetCurrentSecretVersionAsync(
        ConnectedIdentity identity,
        Uri vaultUri,
        string objectName,
        CancellationToken cancellationToken)
    {
        var credential = await credentialProvider.GetCredentialAsync(
            identity,
            cancellationToken);
        var token = await GetKeyVaultTokenAsync(
            credential,
            identity.HomeTenantId,
            cancellationToken);
        using var response = await SendAsync(
            HttpMethod.Get,
            CreateDataPlaneUri(vaultUri, $"secrets/{Escape(objectName)}"),
            token.Token,
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(
            response,
            "Current secret-version read",
            cancellationToken);
        return await ReadProviderVersionAsync(
            response,
            cancellationToken);
    }

    public async Task EnsureAuthorizedAsync(
        ConnectedIdentity identity,
        string vaultResourceId,
        GovernedAzureOperation operation,
        CancellationToken cancellationToken)
    {
        var credential = await credentialProvider.GetCredentialAsync(
            identity,
            cancellationToken);
        var action = operation switch
        {
            GovernedAzureOperation.CreateSecret or
                GovernedAzureOperation.CreateSecretVersion =>
                "Microsoft.KeyVault/vaults/secrets/setSecret/action",
            GovernedAzureOperation.CreateSoftwareKeyVersion =>
                "Microsoft.KeyVault/vaults/keys/create/action",
            GovernedAzureOperation.StartCertificatePolicy =>
                "Microsoft.KeyVault/vaults/certificates/create/action",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        if (!await _authorizationEvaluator.IsCallerDataActionAllowedAsync(
                credential,
                identity,
                vaultResourceId,
                action,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Azure effective caller-permission evidence does not grant the exact requested Key Vault data action.");
        }
    }

    public async Task<GovernedMutationResult> ExecuteAsync(
        ConnectedIdentity identity,
        GovernedMutationPreview preview,
        SensitiveValue? sensitiveValue,
        CancellationToken cancellationToken)
    {
        var credential = await credentialProvider.GetCredentialAsync(
            identity,
            cancellationToken);
        var token = await GetKeyVaultTokenAsync(
            credential,
            identity.HomeTenantId,
            cancellationToken);
        return preview.Operation switch
        {
            GovernedAzureOperation.CreateSecret =>
                await CreateSecretAsync(
                    preview,
                    sensitiveValue!,
                    token.Token,
                    requireAbsent: true,
                    cancellationToken),
            GovernedAzureOperation.CreateSecretVersion =>
                await CreateSecretAsync(
                    preview,
                    sensitiveValue!,
                    token.Token,
                    requireAbsent: false,
                    cancellationToken),
            GovernedAzureOperation.CreateSoftwareKeyVersion =>
                await CreateSoftwareKeyVersionAsync(
                    preview,
                    token.Token,
                    cancellationToken),
            GovernedAzureOperation.StartCertificatePolicy =>
                await StartCertificatePolicyAsync(
                    preview,
                    token.Token,
                    cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(preview)),
        };
    }

    private async Task<GovernedMutationResult> CreateSecretAsync(
        GovernedMutationPreview preview,
        SensitiveValue sensitiveValue,
        string bearerToken,
        bool requireAbsent,
        CancellationToken cancellationToken)
    {
        var currentVersion = await TryGetCurrentSecretVersionAsync(
            preview,
            bearerToken,
            cancellationToken);
        if (requireAbsent && currentVersion is not null)
        {
            throw new GovernedMutationConflictException(
                "A secret with this name already exists. No mutation was submitted.");
        }
        if (!requireAbsent &&
            !string.Equals(
                currentVersion,
                preview.ExpectedCurrentVersion,
                StringComparison.Ordinal))
        {
            throw new GovernedMutationConflictException(
                "The current secret version changed after preview. No mutation was submitted.");
        }

        var body = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(body))
        {
            writer.WriteStartObject();
            writer.WriteString("value", sensitiveValue.Reveal());
            writer.WriteEndObject();
        }
        var requestBytes = body.WrittenSpan.ToArray();
        try
        {
            using var content = new ByteArrayContent(requestBytes);
            content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");
            using var response = await SendAsync(
                HttpMethod.Put,
                CreateDataPlaneUri(
                    preview.VaultUri,
                    $"secrets/{Escape(preview.ObjectName)}"),
                bearerToken,
                content,
                cancellationToken);
            await EnsureSuccessAsync(
                response,
                "Secret version creation",
                cancellationToken);
            var version = await ReadProviderVersionAsync(
                response,
                cancellationToken);
            return new GovernedMutationResult(
                preview.Operation,
                preview.ObjectName,
                version,
                "Azure created one immutable secret version.",
                preview.RecoveryGuidance);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(requestBytes);
            body.Clear();
        }
    }

    private async Task<GovernedMutationResult>
        CreateSoftwareKeyVersionAsync(
            GovernedMutationPreview preview,
            string bearerToken,
            CancellationToken cancellationToken)
    {
        using var content = JsonContent(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("kty", "RSA");
                writer.WriteNumber("key_size", 3072);
                writer.WriteStartArray("key_ops");
                writer.WriteStringValue("encrypt");
                writer.WriteStringValue("decrypt");
                writer.WriteStringValue("wrapKey");
                writer.WriteStringValue("unwrapKey");
                writer.WriteEndArray();
                writer.WriteStartObject("attributes");
                writer.WriteBoolean("enabled", true);
                writer.WriteEndObject();
                writer.WriteEndObject();
            });
        using var response = await SendAsync(
            HttpMethod.Post,
            CreateDataPlaneUri(
                preview.VaultUri,
                $"keys/{Escape(preview.ObjectName)}/create"),
            bearerToken,
            content,
            cancellationToken);
        await EnsureSuccessAsync(
            response,
            "Software key-version creation",
            cancellationToken);
        var version = await ReadProviderVersionAsync(
            response,
            cancellationToken);
        return new GovernedMutationResult(
            preview.Operation,
            preview.ObjectName,
            version,
            "Azure created one software-protected RSA-3072 key version.",
            preview.RecoveryGuidance);
    }

    private async Task<GovernedMutationResult>
        StartCertificatePolicyAsync(
            GovernedMutationPreview preview,
            string bearerToken,
            CancellationToken cancellationToken)
    {
        using var content = JsonContent(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteStartObject("policy");
                writer.WriteStartObject("key_props");
                writer.WriteBoolean("exportable", false);
                writer.WriteString("kty", "RSA");
                writer.WriteNumber("key_size", 3072);
                writer.WriteBoolean("reuse_key", false);
                writer.WriteEndObject();
                writer.WriteStartObject("secret_props");
                writer.WriteString(
                    "contentType",
                    "application/x-pkcs12");
                writer.WriteEndObject();
                writer.WriteStartObject("x509_props");
                writer.WriteString(
                    "subject",
                    $"CN={preview.ObjectName}");
                writer.WriteNumber("validity_months", 12);
                writer.WriteEndObject();
                writer.WriteStartObject("issuer");
                writer.WriteString("name", "Self");
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            });
        using var response = await SendAsync(
            HttpMethod.Post,
            CreateDataPlaneUri(
                preview.VaultUri,
                $"certificates/{Escape(preview.ObjectName)}/create"),
            bearerToken,
            content,
            cancellationToken);
        await EnsureSuccessAsync(
            response,
            "Certificate-policy operation",
            cancellationToken);
        var operationId = response.Headers.TryGetValues(
                "Azure-AsyncOperation",
                out var values)
            ? values.FirstOrDefault() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(operationId))
            operationId = $"pending:{preview.Id:N}";
        return new GovernedMutationResult(
            preview.Operation,
            preview.ObjectName,
            operationId,
            "Azure accepted one certificate-policy operation.",
            preview.RecoveryGuidance);
    }

    private async Task<string?> TryGetCurrentSecretVersionAsync(
        GovernedMutationPreview preview,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            CreateDataPlaneUri(
                preview.VaultUri,
                $"secrets/{Escape(preview.ObjectName)}"),
            bearerToken,
            content: null,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(
            response,
            "Current secret-version concurrency read",
            cancellationToken);
        return await ReadProviderVersionAsync(
            response,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        string bearerToken,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = content,
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        return await keyVaultClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        await response.Content.LoadIntoBufferAsync(
            64 * 1024,
            cancellationToken);
        throw new HttpRequestException(
            $"{operation} failed with HTTP {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private static async Task<string> ReadProviderVersionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            new JsonDocumentOptions { MaxDepth = 16 },
            cancellationToken);
        if (!document.RootElement.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(idElement.GetString(), UriKind.Absolute, out var id))
        {
            throw new InvalidDataException(
                "Azure returned a mutation response without a valid resource identifier.");
        }
        var segments = id.Segments;
        if (segments.Length < 4)
            throw new InvalidDataException(
                "Azure returned a mutation response without a provider version.");
        return segments[^1].Trim('/');
    }

    private static async Task<AccessToken> GetKeyVaultTokenAsync(
        TokenCredential credential,
        string tenantId,
        CancellationToken cancellationToken) =>
        await credential.GetTokenAsync(
            new TokenRequestContext(
                ["https://vault.azure.net/.default"],
                tenantId: tenantId),
            cancellationToken);

    private static Uri CreateDataPlaneUri(
        Uri vaultUri,
        string relativePath)
    {
        if (vaultUri.Scheme != Uri.UriSchemeHttps ||
            !vaultUri.Host.EndsWith(
                ".vault.azure.net",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The Azure Key Vault data-plane endpoint is not trusted.");
        }
        return new Uri(
            vaultUri,
            $"{relativePath}?api-version={ApiVersion}");
    }

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);

    private static ByteArrayContent JsonContent(
        Action<Utf8JsonWriter> write)
    {
        var body = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(body))
            write(writer);
        var content = new ByteArrayContent(body.WrittenSpan.ToArray());
        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");
        return content;
    }
}
