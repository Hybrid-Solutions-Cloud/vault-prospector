using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ManagedServiceIdentities;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.Azure;

public sealed class WorkloadIdentityDiscoveryService : IWorkloadIdentityAdministrationService
{
    private const int MaximumGraphPages = 10;
    private const int MaximumGraphCandidates = 1_000;
    private static readonly Uri GraphServicePrincipalsUri = new(
        "https://graph.microsoft.com/v1.0/servicePrincipals" +
        "?$select=id,appId,displayName,servicePrincipalType,accountEnabled&$top=100");

    private readonly Func<ConnectedIdentity, CancellationToken, Task<TokenCredential>> _credentialResolver;
    private readonly HttpClient _graphClient;
    private readonly AzureAuthorizationEvidenceEvaluator _authorizationEvaluator;

    public WorkloadIdentityDiscoveryService(
        MsalIdentityProvider identityProvider,
        HttpClient graphClient,
        HttpClient authorizationClient)
        : this(
            identityProvider.GetCredentialAsync,
            graphClient,
            new AzureAuthorizationEvidenceEvaluator(authorizationClient))
    {
    }

    public WorkloadIdentityDiscoveryService(
        TokenCredential credential,
        HttpClient? graphClient = null,
        HttpClient? authorizationClient = null)
        : this(
            (_, _) => Task.FromResult(credential),
            graphClient ?? new HttpClient(),
            new AzureAuthorizationEvidenceEvaluator(
                authorizationClient ?? graphClient ?? new HttpClient()))
    {
    }

    private WorkloadIdentityDiscoveryService(
        Func<ConnectedIdentity, CancellationToken, Task<TokenCredential>> credentialResolver,
        HttpClient graphClient,
        AzureAuthorizationEvidenceEvaluator authorizationEvaluator)
    {
        _credentialResolver = credentialResolver;
        _graphClient = graphClient;
        _authorizationEvaluator = authorizationEvaluator;
    }

    public async Task<IReadOnlyList<WorkloadIdentityCandidate>> ListManagedIdentitiesAsync(
        ConnectedIdentity administrator,
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        EnsureInteractiveAdministrator(administrator);
        var normalizedSubscriptionId = NormalizeGuid(subscriptionId, nameof(subscriptionId));
        var credential = await _credentialResolver(administrator, cancellationToken);
        var armClient = new ArmClient(credential);
        var subscription = armClient.GetSubscriptionResource(
            new ResourceIdentifier($"/subscriptions/{normalizedSubscriptionId}"));
        var candidates = new List<WorkloadIdentityCandidate>();

        await foreach (var identity in subscription
            .GetUserAssignedIdentitiesAsync(cancellationToken)
            .WithCancellation(cancellationToken))
        {
            candidates.Add(new WorkloadIdentityCandidate(
                "User-assigned managed identity",
                administrator.HomeTenantId,
                normalizedSubscriptionId,
                identity.Id.ResourceGroupName ?? string.Empty,
                identity.Data.Name,
                identity.Id.ToString(),
                identity.Data.ClientId?.ToString("D") ?? string.Empty,
                identity.Data.PrincipalId?.ToString("D") ?? string.Empty,
                identity.Data.Location.Name,
                true,
                ReadOnlyDiscoveryAssessment()));
        }

        return candidates;
    }

    public async Task<IReadOnlyList<WorkloadIdentityCandidate>> ListServicePrincipalsAsync(
        ConnectedIdentity administrator,
        CancellationToken cancellationToken)
    {
        EnsureInteractiveAdministrator(administrator);
        var credential = await _credentialResolver(administrator, cancellationToken);
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(
                AzureAuthenticationScopes.GraphDirectoryRead.ToArray(),
                tenantId: administrator.HomeTenantId),
            cancellationToken);
        var candidates = new List<WorkloadIdentityCandidate>();
        Uri? pageUri = GraphServicePrincipalsUri;

        for (var page = 0; pageUri is not null && page < MaximumGraphPages; page++)
        {
            EnsureTrustedGraphUri(pageUri);
            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var response = await _graphClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Microsoft Graph service-principal discovery failed with HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);

            using var document = await BoundedJsonDocument.ReadAsync(
                response.Content,
                "Microsoft Graph",
                cancellationToken);
            if (!document.RootElement.TryGetProperty("value", out var values) ||
                values.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    "Microsoft Graph returned an invalid service-principal list.");
            }

            foreach (var value in values.EnumerateArray())
            {
                if (candidates.Count >= MaximumGraphCandidates)
                    throw new InvalidDataException(
                        "Microsoft Graph returned more service principals than the safe display limit.");
                candidates.Add(ToServicePrincipalCandidate(administrator.HomeTenantId, value));
            }

            pageUri = ReadNextGraphPage(document.RootElement);
        }

        if (pageUri is not null)
            throw new InvalidDataException(
                "Microsoft Graph pagination exceeded the safe page limit.");
        return candidates;
    }

    public async Task<WorkloadIdentityCandidate> AssessPermissionsAsync(
        ConnectedIdentity administrator,
        WorkloadIdentityCandidate candidate,
        string keyVaultResourceId,
        CancellationToken cancellationToken)
    {
        EnsureInteractiveAdministrator(administrator);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!string.Equals(
                administrator.HomeTenantId,
                candidate.TenantId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The selected administrator and workload identity must belong to the same tenant.");
        }

        var credential = await _credentialResolver(administrator, cancellationToken);
        var permissions = await _authorizationEvaluator.AssessAsync(
            credential,
            administrator,
            candidate,
            keyVaultResourceId,
            cancellationToken);
        return candidate with { Permissions = permissions };
    }

    WorkloadIdentityProvisioningPlan IWorkloadIdentityAdministrationService.BuildManagedIdentityDryRun(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string identityName,
        string? keyVaultResourceId,
        string? keyVaultRoleDefinitionId) =>
        BuildManagedIdentityDryRunCore(
            tenantId,
            subscriptionId,
            resourceGroupName,
            identityName,
            keyVaultResourceId,
            keyVaultRoleDefinitionId);

    WorkloadIdentityProvisioningPlan IWorkloadIdentityAdministrationService.BuildServicePrincipalDryRun(
        string tenantId,
        string identityName,
        string? keyVaultResourceId,
        string? keyVaultRoleDefinitionId) =>
        BuildServicePrincipalDryRunCore(
            tenantId,
            identityName,
            keyVaultResourceId,
            keyVaultRoleDefinitionId);

    private static WorkloadIdentityProvisioningPlan BuildServicePrincipalDryRunCore(
        string tenantId,
        string identityName,
        string? keyVaultResourceId,
        string? keyVaultRoleDefinitionId)
    {
        var normalizedTenantId = NormalizeGuid(tenantId, nameof(tenantId));
        var normalizedName = NormalizeAzureName(identityName, nameof(identityName));
        var operations = new List<PlannedAzureOperation>
        {
            new(
                "Create",
                "Microsoft.Graph/applications",
                $"/tenants/{normalizedTenantId}",
                $"Create one application registration named {normalizedName} without a client secret."),
            new(
                "Create",
                "Microsoft.Graph/servicePrincipals",
                $"/tenants/{normalizedTenantId}",
                "Create one enterprise application for the new registration without granting directory or Azure roles."),
        };
        var subscriptionId = string.Empty;
        var resourceGroup = string.Empty;
        AppendOptionalRoleAssignment(
            operations,
            keyVaultResourceId,
            keyVaultRoleDefinitionId,
            out subscriptionId,
            out resourceGroup);

        return new WorkloadIdentityProvisioningPlan(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "ServicePrincipal",
            normalizedTenantId,
            subscriptionId,
            resourceGroup,
            normalizedName,
            operations);
    }

    public static WorkloadIdentityProvisioningPlan BuildManagedIdentityDryRun(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string identityName,
        string? keyVaultResourceId = null,
        string? keyVaultRoleDefinitionId = null) =>
        BuildManagedIdentityDryRunCore(
            tenantId,
            subscriptionId,
            resourceGroupName,
            identityName,
            keyVaultResourceId,
            keyVaultRoleDefinitionId);

    public static WorkloadIdentityProvisioningPlan BuildServicePrincipalDryRun(
        string tenantId,
        string identityName,
        string? keyVaultResourceId = null,
        string? keyVaultRoleDefinitionId = null) =>
        BuildServicePrincipalDryRunCore(
            tenantId,
            identityName,
            keyVaultResourceId,
            keyVaultRoleDefinitionId);

    private static WorkloadIdentityProvisioningPlan BuildManagedIdentityDryRunCore(
        string tenantId,
        string subscriptionId,
        string resourceGroupName,
        string identityName,
        string? keyVaultResourceId,
        string? keyVaultRoleDefinitionId)
    {
        var normalizedTenantId = NormalizeGuid(tenantId, nameof(tenantId));
        var normalizedSubscriptionId = NormalizeGuid(subscriptionId, nameof(subscriptionId));
        var normalizedResourceGroup = NormalizeAzureName(resourceGroupName, nameof(resourceGroupName));
        var normalizedIdentityName = NormalizeAzureName(identityName, nameof(identityName));
        var identityScope =
            $"/subscriptions/{normalizedSubscriptionId}/resourceGroups/{normalizedResourceGroup}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{normalizedIdentityName}";
        List<PlannedAzureOperation> operations =
        [
            new(
                "Create",
                "Microsoft.ManagedIdentity/userAssignedIdentities",
                identityScope,
                "Create one user-assigned managed identity without attaching it to compute or granting Azure permissions."),
        ];

        AppendOptionalRoleAssignment(
            operations,
            keyVaultResourceId,
            keyVaultRoleDefinitionId,
            out var vaultSubscriptionId,
            out _);
        if (!string.IsNullOrEmpty(vaultSubscriptionId) &&
            !string.Equals(
                vaultSubscriptionId,
                normalizedSubscriptionId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The exact Key Vault role scope must be in the planned managed identity subscription.",
                nameof(keyVaultResourceId));
        }

        return new WorkloadIdentityProvisioningPlan(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "UserAssignedManagedIdentity",
            normalizedTenantId,
            normalizedSubscriptionId,
            normalizedResourceGroup,
            normalizedIdentityName,
            operations);
    }

    private static void AppendOptionalRoleAssignment(
        List<PlannedAzureOperation> operations,
        string? keyVaultResourceId,
        string? keyVaultRoleDefinitionId,
        out string subscriptionId,
        out string resourceGroup)
    {
        subscriptionId = string.Empty;
        resourceGroup = string.Empty;
        if (string.IsNullOrWhiteSpace(keyVaultResourceId) &&
            string.IsNullOrWhiteSpace(keyVaultRoleDefinitionId))
            return;
        if (string.IsNullOrWhiteSpace(keyVaultResourceId) ||
            string.IsNullOrWhiteSpace(keyVaultRoleDefinitionId))
        {
            throw new ArgumentException(
                "A Key Vault resource ID and role-definition ID must be supplied together.");
        }

        var vaultScope = NormalizeResourceId(
            keyVaultResourceId,
            "Microsoft.KeyVault/vaults",
            nameof(keyVaultResourceId));
        var roleDefinition = NormalizeResourceId(
            keyVaultRoleDefinitionId,
            "Microsoft.Authorization/roleDefinitions",
            nameof(keyVaultRoleDefinitionId));
        var vaultIdentifier = new ResourceIdentifier(vaultScope);
        var roleIdentifier = new ResourceIdentifier(roleDefinition);
        if (!string.Equals(
            vaultIdentifier.SubscriptionId,
            roleIdentifier.SubscriptionId,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The role definition and Key Vault must belong to the same subscription.",
                nameof(keyVaultRoleDefinitionId));
        }
        subscriptionId = vaultIdentifier.SubscriptionId ?? string.Empty;
        resourceGroup = vaultIdentifier.ResourceGroupName ?? string.Empty;
        operations.Add(new PlannedAzureOperation(
            "AssignRole",
            "Microsoft.Authorization/roleAssignments",
            vaultScope,
            $"Assign role definition {roleDefinition} to the new identity at exactly this Key Vault scope."));
    }

    private static WorkloadIdentityCandidate ToServicePrincipalCandidate(
        string tenantId,
        JsonElement value)
    {
        var principalId = RequiredString(value, "id");
        var clientId = RequiredString(value, "appId");
        if (!Guid.TryParse(principalId, out var parsedPrincipalId) ||
            !Guid.TryParse(clientId, out var parsedClientId))
        {
            throw new InvalidDataException(
                "Microsoft Graph returned a service principal with an invalid identifier.");
        }

        var enabled = !value.TryGetProperty("accountEnabled", out var enabledElement) ||
            enabledElement.ValueKind == JsonValueKind.Null ||
            enabledElement.GetBoolean();
        var displayName = OptionalString(value, "displayName");
        var servicePrincipalType = OptionalString(value, "servicePrincipalType");
        var permissions = ReadOnlyDiscoveryAssessment() with
        {
            AttachOrUse = enabled
                ? "Not proven — credential ownership and target attachment were not evaluated."
                : "Unavailable — Microsoft Graph reports this service principal disabled.",
        };
        return new WorkloadIdentityCandidate(
            string.IsNullOrWhiteSpace(servicePrincipalType)
                ? "Service principal"
                : $"Service principal ({servicePrincipalType})",
            tenantId,
            string.Empty,
            string.Empty,
            string.IsNullOrWhiteSpace(displayName)
                ? parsedClientId.ToString("D")
                : displayName,
            $"/tenants/{tenantId}/servicePrincipals/{parsedPrincipalId:D}",
            parsedClientId.ToString("D"),
            parsedPrincipalId.ToString("D"),
            string.Empty,
            enabled,
            permissions);
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = OptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Microsoft Graph omitted required service-principal field {propertyName}.");
        return value;
    }

    private static string OptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static Uri? ReadNextGraphPage(JsonElement root)
    {
        if (!root.TryGetProperty("@odata.nextLink", out var next) ||
            next.ValueKind == JsonValueKind.Null)
            return null;
        if (next.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(next.GetString(), UriKind.Absolute, out var nextUri))
            throw new InvalidDataException("Microsoft Graph returned an invalid next-page link.");
        EnsureTrustedGraphUri(nextUri);
        return nextUri;
    }

    private static void EnsureTrustedGraphUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort)
        {
            throw new InvalidDataException(
                "Microsoft Graph returned an untrusted next-page location.");
        }
    }

    private static WorkloadPermissionAssessment ReadOnlyDiscoveryAssessment() =>
        new(
            "Confirmed — returned by the selected identity's authorized listing operation.",
            "Not proven — target attachment and credential ownership were not evaluated.",
            "Not proven — Vault Prospector requested no identity write permission.",
            "Not proven — connect the candidate and synchronize an exact vault to observe data-plane access.",
            "Unavailable — Azure mutation is disabled by application policy.");

    private static void EnsureInteractiveAdministrator(ConnectedIdentity administrator)
    {
        if (administrator.Type != IdentityType.InteractiveUser ||
            !administrator.IsEnabled ||
            administrator.AuthenticationState != AuthenticationState.Ready)
        {
            throw new InvalidOperationException(
                "Select an enabled, ready interactive administrator identity.");
        }
    }

    private static string NormalizeGuid(string value, string parameterName)
    {
        if (!Guid.TryParse(value, out var parsed))
            throw new ArgumentException("A GUID is required.", parameterName);
        return parsed.ToString("D");
    }

    private static string NormalizeAzureName(string value, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 90 ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.' and not '(' and not ')'))
        {
            throw new ArgumentException(
                "The Azure resource name contains unsupported characters or has an invalid length.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizeResourceId(
        string value,
        string expectedResourceType,
        string parameterName)
    {
        var resourceId = new ResourceIdentifier(value.Trim());
        if (resourceId.SubscriptionId is null)
            throw new ArgumentException(
                "A subscription-scoped Azure resource ID is required.",
                parameterName);
        if (!string.Equals(
            resourceId.ResourceType.ToString(),
            expectedResourceType,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"An exact {expectedResourceType} resource ID is required.",
                parameterName);
        }
        return resourceId.ToString();
    }
}
