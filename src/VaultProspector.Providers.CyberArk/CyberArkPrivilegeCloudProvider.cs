using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.CyberArk;

public sealed class CyberArkProviderException(
    string category,
    string safeMessage,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null)
    : Exception(safeMessage, innerException)
{
    public string Category { get; } = category;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class CyberArkPrivilegeCloudProvider(HttpClient httpClient, IClock clock)
    : ICyberArkProvider
{
    private const int MaximumResponseBytes = 4 * 1024 * 1024;
    private const int MaximumSecretBytes = 1024 * 1024;
    private const int MaximumPages = 100;
    private const int MaximumItems = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task ValidateAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        using var session = await AuthenticateAsync(
            profile,
            clientCredential,
            cancellationToken);
        using var response = await SendPrivilegeCloudAsync(
            profile,
            session.Token,
            HttpMethod.Get,
            "Safes?limit=1",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, "validate", cancellationToken);
        _ = await ReadJsonAsync<PagedResponse<SafeDto>>(
            response,
            MaximumResponseBytes,
            "validate",
            cancellationToken);
    }

    public async Task<CyberArkDiscoverySnapshot> DiscoverAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        using var session = await AuthenticateAsync(
            profile,
            clientCredential,
            cancellationToken);
        var errors = new List<ProviderError>();
        var safes = new List<CyberArkSafe>();
        var accounts = new List<CyberArkAccount>();
        var versions = new List<CyberArkSecretVersion>();
        var permissions = new List<CyberArkSafePermissionEvidence>();

        foreach (var safe in await ListPagesAsync<SafeDto>(
                     profile,
                     session.Token,
                     "Safes?limit=100",
                     "list_safes",
                     cancellationToken))
        {
            var mappedSafe = MapSafe(profile.Id, safe);
            safes.Add(mappedSafe);
            try
            {
                var permission = await GetPermissionAsync(
                    profile,
                    session.Token,
                    mappedSafe.SafeId,
                    cancellationToken);
                if (permission is not null)
                    permissions.Add(permission);
            }
            catch (CyberArkProviderException exception)
            {
                errors.Add(new ProviderError(
                    mappedSafe.SafeId,
                    exception.Category,
                    "CyberArk safe permission evidence was unavailable."));
            }
        }

        foreach (var account in await ListPagesAsync<AccountDto>(
                     profile,
                     session.Token,
                     "Accounts?limit=100",
                     "list_accounts",
                     cancellationToken))
        {
            var mappedAccount = MapAccount(profile.Id, account);
            accounts.Add(mappedAccount);
            try
            {
                var accountVersions = await GetVersionsAsync(
                    profile,
                    session.Token,
                    mappedAccount.AccountId,
                    cancellationToken);
                if (versions.Count + accountVersions.Count > MaximumItems)
                    throw new CyberArkProviderException(
                        "item_limit",
                        "CyberArk discovery exceeded the supported version limit.");
                versions.AddRange(accountVersions);
            }
            catch (CyberArkProviderException exception)
            {
                errors.Add(new ProviderError(
                    mappedAccount.AccountId,
                    exception.Category,
                    "CyberArk account version metadata was unavailable."));
            }
        }

        return new CyberArkDiscoverySnapshot(
            safes,
            accounts,
            versions,
            permissions,
            errors,
            clock.UtcNow);
    }

    public async Task<SensitiveValue> RetrieveAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CyberArkAccount account,
        int? versionId,
        string reason,
        string actionType,
        CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        ArgumentNullException.ThrowIfNull(account);
        if (account.ProfileId != profile.Id)
            throw new CyberArkProviderException(
                "source_mismatch",
                "The CyberArk account does not belong to the selected profile.");
        if (account.IsDeletedOrUnavailable)
            throw new CyberArkProviderException(
                "account_unavailable",
                "The selected CyberArk account is unavailable.");
        if (reason.Length > 1_000)
            throw new CyberArkConfigurationException(
                "The retrieval reason cannot exceed 1,000 characters.",
                nameof(reason));
        if (actionType is not ("show" or "copy"))
            throw new CyberArkConfigurationException(
                "The CyberArk action type must be show or copy.",
                nameof(actionType));

        using var session = await AuthenticateAsync(
            profile,
            clientCredential,
            cancellationToken);
        var body = new Dictionary<string, object?>
        {
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ["Version"] = versionId?.ToString(CultureInfo.InvariantCulture),
            ["ActionType"] = actionType,
        };
        using var response = await SendPrivilegeCloudAsync(
            profile,
            session.Token,
            HttpMethod.Post,
            $"Accounts/{Uri.EscapeDataString(account.AccountId)}/Password/Retrieve",
            JsonSerializer.Serialize(body, JsonOptions),
            cancellationToken);
        await EnsureSuccessAsync(response, "retrieve_account", cancellationToken);

        var bytes = await ReadBoundedAsync(
            response.Content,
            MaximumSecretBytes,
            "retrieve_account",
            cancellationToken);
        try
        {
            string? value;
            try
            {
                value = JsonSerializer.Deserialize<string>(bytes, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new CyberArkProviderException(
                    "invalid_response",
                    "CyberArk returned an invalid credential response.",
                    response.StatusCode,
                    exception);
            }

            if (string.IsNullOrEmpty(value))
                throw new CyberArkProviderException(
                    "invalid_response",
                    "CyberArk returned an empty credential response.",
                    response.StatusCode);
            return new SensitiveValue(value);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task<IReadOnlyList<T>> ListPagesAsync<T>(
        CyberArkProfile profile,
        string token,
        string initialPath,
        string operation,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        Uri? next = BuildPrivilegeUri(profile, initialPath);
        for (var page = 0; next is not null; page++)
        {
            if (page >= MaximumPages)
                throw new CyberArkProviderException(
                    "pagination_limit",
                    "CyberArk pagination exceeded the supported page limit.");

            using var response = await SendPrivilegeCloudAsync(
                profile,
                token,
                HttpMethod.Get,
                next,
                null,
                cancellationToken);
            await EnsureSuccessAsync(response, operation, cancellationToken);
            var result = await ReadJsonAsync<PagedResponse<T>>(
                response,
                MaximumResponseBytes,
                operation,
                cancellationToken);
            if (result.Value is null)
                throw new CyberArkProviderException(
                    "invalid_response",
                    "CyberArk returned a page without a value collection.");
            if (items.Count + result.Value.Count > MaximumItems)
                throw new CyberArkProviderException(
                    "item_limit",
                    "CyberArk discovery exceeded the supported item limit.");
            items.AddRange(result.Value);
            next = string.IsNullOrWhiteSpace(result.NextLink)
                ? null
                : ValidateNextLink(profile, result.NextLink);
        }

        return items;
    }

    private async Task<IReadOnlyList<CyberArkSecretVersion>> GetVersionsAsync(
        CyberArkProfile profile,
        string token,
        string accountId,
        CancellationToken cancellationToken)
    {
        using var response = await SendPrivilegeCloudAsync(
            profile,
            token,
            HttpMethod.Get,
            $"Accounts/{Uri.EscapeDataString(accountId)}/Secret/Versions",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, "list_versions", cancellationToken);
        var result = await ReadJsonAsync<VersionsResponse>(
            response,
            MaximumResponseBytes,
            "list_versions",
            cancellationToken);
        if (result.Versions is null || result.Versions.Count > MaximumItems)
            throw new CyberArkProviderException(
                "invalid_response",
                "CyberArk returned an invalid version collection.");
        return result.Versions.Select(version => new CyberArkSecretVersion(
            profile.Id,
            accountId,
            version.VersionId,
            version.IsTemporary,
            version.ModificationDate,
            version.ModifiedBy ?? string.Empty)).ToArray();
    }

    private async Task<CyberArkSafePermissionEvidence?> GetPermissionAsync(
        CyberArkProfile profile,
        string token,
        string safeId,
        CancellationToken cancellationToken)
    {
        using var response = await SendPrivilegeCloudAsync(
            profile,
            token,
            HttpMethod.Get,
            $"Safes/{Uri.EscapeDataString(safeId)}/Members/{Uri.EscapeDataString(profile.ServiceUserName)}",
            null,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, "get_safe_permission", cancellationToken);
        var member = await ReadJsonAsync<MemberDto>(
            response,
            MaximumResponseBytes,
            "get_safe_permission",
            cancellationToken);
        var permission = member.Permissions ?? new PermissionDto();
        return new CyberArkSafePermissionEvidence(
            profile.Id,
            safeId,
            member.MemberName ?? profile.ServiceUserName,
            member.MemberType ?? "Unknown",
            permission.ListAccounts,
            permission.UseAccounts,
            permission.RetrieveAccounts,
            permission.ViewAuditLog,
            permission.AccessWithoutConfirmation,
            permission.RequestsAuthorizationLevel1,
            permission.RequestsAuthorizationLevel2,
            clock.UtcNow,
            "CyberArk direct safe-member API observation");
    }

    private async Task<CyberArkSession> AuthenticateAsync(
        CyberArkProfile profile,
        SensitiveValue clientCredential,
        CancellationToken cancellationToken)
    {
        ValidateProfile(profile);
        ArgumentNullException.ThrowIfNull(clientCredential);
        byte[]? userBytes = null;
        byte[]? credentialBytes = null;
        byte[]? basicBytes = null;
        try
        {
            userBytes = Encoding.UTF8.GetBytes(profile.ServiceUserName);
            credentialBytes = clientCredential.CopyUtf8Bytes();
            basicBytes = new byte[
                userBytes.Length + 1 + credentialBytes.Length];
            userBytes.CopyTo(basicBytes, 0);
            basicBytes[userBytes.Length] = (byte)':';
            credentialBytes.CopyTo(
                basicBytes,
                userBytes.Length + 1);
            using var tokenRequest = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(
                    profile.IdentityUrl,
                    $"Oauth2/Token/{Uri.EscapeDataString(profile.ApplicationName)}"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = "api",
                }),
            };
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(basicBytes));
            using var tokenResponse = await httpClient.SendAsync(
                tokenRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            EnsureResponseStayedOnOrigin(
                tokenResponse,
                profile.IdentityUrl,
                "authenticate");
            await EnsureSuccessAsync(
                tokenResponse,
                "authenticate",
                cancellationToken);
            var tokenResult = await ReadJsonAsync<TokenResponse>(
                tokenResponse,
                MaximumResponseBytes,
                "authenticate",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
                throw new CyberArkProviderException(
                    "invalid_response",
                    "CyberArk Identity did not return an access token.");

            var authorizeUri = BuildAuthorizeUri(profile);
            using var authorizeRequest = new HttpRequestMessage(
                HttpMethod.Get,
                authorizeUri);
            authorizeRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                tokenResult.AccessToken);
            using var authorizeResponse = await httpClient.SendAsync(
                authorizeRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            EnsureResponseStayedOnOrigin(
                authorizeResponse,
                profile.IdentityUrl,
                "authorize");
            if (authorizeResponse.StatusCode != HttpStatusCode.Found ||
                authorizeResponse.Headers.Location is null)
            {
                throw ProviderFailure(authorizeResponse.StatusCode, "authorize");
            }

            var redirect = authorizeResponse.Headers.Location.IsAbsoluteUri
                ? authorizeResponse.Headers.Location
                : new Uri(authorizeUri, authorizeResponse.Headers.Location);
            if (!string.Equals(
                    redirect.GetLeftPart(UriPartial.Path),
                    "https://cyberark.cloud/redirect",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new CyberArkProviderException(
                    "untrusted_redirect",
                    "CyberArk Identity returned an untrusted authorization redirect.");
            }

            var idToken = ParseFragmentValue(redirect.Fragment, "id_token");
            if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > MaximumResponseBytes)
                throw new CyberArkProviderException(
                    "invalid_response",
                    "CyberArk Identity did not return a valid platform token.");
            return new CyberArkSession(idToken);
        }
        finally
        {
            if (userBytes is not null)
                CryptographicOperations.ZeroMemory(userBytes);
            if (credentialBytes is not null)
                CryptographicOperations.ZeroMemory(credentialBytes);
            if (basicBytes is not null)
                CryptographicOperations.ZeroMemory(basicBytes);
        }
    }

    private async Task<HttpResponseMessage> SendPrivilegeCloudAsync(
        CyberArkProfile profile,
        string token,
        HttpMethod method,
        string path,
        string? json,
        CancellationToken cancellationToken) =>
        await SendPrivilegeCloudAsync(
            profile,
            token,
            method,
            BuildPrivilegeUri(profile, path),
            json,
            cancellationToken);

    private async Task<HttpResponseMessage> SendPrivilegeCloudAsync(
        CyberArkProfile profile,
        string token,
        HttpMethod method,
        Uri uri,
        string? json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            "application/json"));
        if (json is not null)
            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");
        var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        try
        {
            EnsureResponseStayedOnOrigin(
                response,
                profile.PrivilegeCloudUrl,
                "privilege_cloud");
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static Uri BuildPrivilegeUri(
        CyberArkProfile profile,
        string path) =>
        new(
            profile.PrivilegeCloudUrl,
            $"PasswordVault/API/{path.TrimStart('/')}");

    private static Uri ValidateNextLink(
        CyberArkProfile profile,
        string nextLink)
    {
        var uri = Uri.TryCreate(nextLink, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(profile.PrivilegeCloudUrl, nextLink);
        if (!IsSameOrigin(uri, profile.PrivilegeCloudUrl) ||
            !uri.AbsolutePath.StartsWith(
                "/PasswordVault/API/",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new CyberArkProviderException(
                "untrusted_pagination",
                "CyberArk returned an untrusted pagination link.");
        }

        return uri;
    }

    private static Uri BuildAuthorizeUri(CyberArkProfile profile)
    {
        var builder = new UriBuilder(
            new Uri(
                profile.IdentityUrl,
                $"OAuth2/Authorize/{Uri.EscapeDataString(profile.ApplicationName)}"))
        {
            Query = string.Join(
                "&",
                new Dictionary<string, string>
                {
                    ["client_id"] = profile.ApplicationName,
                    ["response_type"] = "id_token",
                    ["scope"] = "openid profile api",
                    ["redirect_uri"] = "https://cyberark.cloud/redirect",
                }.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")),
        };
        return builder.Uri;
    }

    private static void ValidateProfile(CyberArkProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateEndpoint(
            profile.IdentityUrl,
            ".id.cyberark.cloud",
            "identity");
        ValidateEndpoint(
            profile.PrivilegeCloudUrl,
            ".privilegecloud.cyberark.cloud",
            "Privilege Cloud");
        if (string.IsNullOrWhiteSpace(profile.DisplayName) ||
            profile.DisplayName.Length > 128)
            throw new CyberArkConfigurationException(
                "The CyberArk profile display name is required and cannot exceed 128 characters.",
                nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.ServiceUserName) ||
            profile.ServiceUserName.Length > 256 ||
            profile.ServiceUserName.Contains(
                ':',
                StringComparison.Ordinal))
            throw new CyberArkConfigurationException(
                "The CyberArk service user name is invalid.",
                nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.ApplicationName) ||
            profile.ApplicationName.Length > 128 ||
            profile.ApplicationName.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '-' and not '_'))
            throw new CyberArkConfigurationException(
                "The CyberArk application name is invalid.",
                nameof(profile));
    }

    private static void ValidateEndpoint(
        Uri endpoint,
        string requiredSuffix,
        string label)
    {
        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !endpoint.IsDefaultPort ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment) ||
            endpoint.AbsolutePath != "/" ||
            !endpoint.IdnHost.EndsWith(
                requiredSuffix,
                StringComparison.OrdinalIgnoreCase) ||
            endpoint.IdnHost.Length <= requiredSuffix.Length)
        {
            throw new CyberArkConfigurationException(
                $"The CyberArk {label} endpoint must be a root HTTPS URL on the supported CyberArk cloud domain.",
                nameof(endpoint));
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
            throw ProviderFailure(response.StatusCode, operation);
        var bytes = await ReadBoundedAsync(
            response.Content,
            MaximumResponseBytes,
            operation,
            cancellationToken);
        CryptographicOperations.ZeroMemory(bytes);
        throw ProviderFailure(response.StatusCode, operation);
    }

    private static CyberArkProviderException ProviderFailure(
        HttpStatusCode statusCode,
        string operation)
    {
        var category = statusCode switch
        {
            HttpStatusCode.Unauthorized => "authentication_required",
            HttpStatusCode.Forbidden => "permission_denied",
            HttpStatusCode.NotFound => "not_found",
            HttpStatusCode.TooManyRequests => "throttled",
            _ when (int)statusCode >= 500 => "service_unavailable",
            _ => "request_failed",
        };
        return new CyberArkProviderException(
            category,
            $"CyberArk {operation} failed with HTTP {(int)statusCode}.",
            statusCode);
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        int maximumBytes,
        string operation,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedAsync(
            response.Content,
            maximumBytes,
            operation,
            cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions)
                ?? throw new CyberArkProviderException(
                    "invalid_response",
                    $"CyberArk {operation} returned an empty response.",
                    response.StatusCode);
        }
        catch (JsonException exception)
        {
            throw new CyberArkProviderException(
                "invalid_response",
                $"CyberArk {operation} returned invalid JSON.",
                response.StatusCode,
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        string operation,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 &&
            content.Headers.ContentLength > maximumBytes)
            throw new CyberArkProviderException(
                "response_too_large",
                $"CyberArk {operation} returned an oversized response.");
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(chunk, cancellationToken);
                if (read == 0)
                    break;
                if (buffer.Length + read > maximumBytes)
                    throw new CyberArkProviderException(
                        "response_too_large",
                        $"CyberArk {operation} returned an oversized response.");
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chunk);
        }
    }

    private static void EnsureResponseStayedOnOrigin(
        HttpResponseMessage response,
        Uri expectedOrigin,
        string operation)
    {
        var requestUri = response.RequestMessage?.RequestUri;
        if (requestUri is null || !IsSameOrigin(requestUri, expectedOrigin))
            throw new CyberArkProviderException(
                "untrusted_redirect",
                $"CyberArk {operation} left the configured endpoint.");
    }

    private static bool IsSameOrigin(Uri left, Uri right) =>
        left.Scheme == Uri.UriSchemeHttps &&
        right.Scheme == Uri.UriSchemeHttps &&
        left.IsDefaultPort &&
        right.IsDefaultPort &&
        string.Equals(
            left.IdnHost,
            right.IdnHost,
            StringComparison.OrdinalIgnoreCase);

    private static string? ParseFragmentValue(
        string fragment,
        string name)
    {
        foreach (var part in fragment.TrimStart('#').Split(
                     '&',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;
            if (string.Equals(
                    Uri.UnescapeDataString(part[..separator]),
                    name,
                    StringComparison.Ordinal))
                return Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return null;
    }

    private static CyberArkSafe MapSafe(Guid profileId, SafeDto safe) =>
        new(
            profileId,
            Required(safe.SafeUrlId, "safe id"),
            Required(safe.SafeName, "safe name"),
            safe.Description ?? string.Empty,
            safe.Location ?? "\\",
            safe.NumberOfDaysRetention,
            safe.NumberOfVersionsRetention,
            safe.OlacEnabled,
            UnixTime(safe.CreationTime),
            UnixTime(safe.LastModificationTime));

    private CyberArkAccount MapAccount(Guid profileId, AccountDto account)
    {
        var secretType = account.SecretType?.ToLowerInvariant() switch
        {
            "password" => CyberArkSecretType.Password,
            "key" => CyberArkSecretType.Key,
            _ => CyberArkSecretType.Unknown,
        };
        var fingerprintInput = string.Join(
            '\u001f',
            account.SafeName,
            account.Name,
            account.UserName,
            account.Address,
            account.PlatformId,
            account.SecretType,
            account.Status,
            account.CreatedTime?.ToString(CultureInfo.InvariantCulture),
            account.CategoryModificationTime?.ToString(
                CultureInfo.InvariantCulture));
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
        return new CyberArkAccount(
            profileId,
            Required(account.Id, "account id"),
            Required(account.SafeName, "account safe name"),
            Required(account.Name, "account name"),
            account.UserName,
            account.Address,
            account.PlatformId,
            secretType,
            account.Status,
            UnixTime(account.CreatedTime),
            UnixTime(account.CategoryModificationTime),
            fingerprint,
            clock.UtcNow);
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new CyberArkProviderException(
                "invalid_response",
                $"CyberArk returned an object without a required {field}.")
            : value;

    private static DateTimeOffset? UnixTime(long? value) =>
        value is null ? null : DateTimeOffset.FromUnixTimeSeconds(value.Value);

    private sealed class CyberArkSession(string token) : IDisposable
    {
        public string Token { get; } = token;
        public void Dispose()
        {
            // .NET authorization headers require immutable strings. The token is deliberately
            // scoped to one operation and is never cached or logged.
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record PagedResponse<T>(
        [property: JsonPropertyName("value")] List<T>? Value,
        [property: JsonPropertyName("nextLink")] string? NextLink);

    private sealed record VersionsResponse(
        [property: JsonPropertyName("versions")] List<VersionDto>? Versions);

    private sealed record SafeDto(
        string? SafeUrlId,
        string? SafeName,
        string? Description,
        string? Location,
        int? NumberOfDaysRetention,
        int? NumberOfVersionsRetention,
        bool OlacEnabled,
        long? CreationTime,
        long? LastModificationTime);

    private sealed record AccountDto(
        string? Id,
        string? Name,
        string? SafeName,
        string? PlatformId,
        string? UserName,
        string? Address,
        string? SecretType,
        string? Status,
        long? CreatedTime,
        long? CategoryModificationTime);

    private sealed record VersionDto(
        bool IsTemporary,
        DateTimeOffset ModificationDate,
        string? ModifiedBy,
        int VersionId);

    private sealed record MemberDto(
        string? MemberName,
        string? MemberType,
        PermissionDto? Permissions);

    private sealed record PermissionDto
    {
        public bool ListAccounts { get; init; }
        public bool UseAccounts { get; init; }
        public bool RetrieveAccounts { get; init; }
        public bool ViewAuditLog { get; init; }
        public bool AccessWithoutConfirmation { get; init; }
        public bool RequestsAuthorizationLevel1 { get; init; }
        public bool RequestsAuthorizationLevel2 { get; init; }
    }
}
