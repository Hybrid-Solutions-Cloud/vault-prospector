using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Providers.Azure;

internal sealed class AzureAuthorizationEvidenceEvaluator
{
    private const int MaximumPages = 20;
    private const int MaximumItems = 2_000;
    private const string AuthorizationApiVersion = "2022-04-01";
    private const string KeyVaultApiVersion = "2023-07-01";
    private const string AllPrincipalsId = "00000000-0000-0000-0000-000000000000";
    private const string ManagedIdentityAttachAction =
        "Microsoft.ManagedIdentity/userAssignedIdentities/example/assign/action";
    private const string ManagedIdentityWriteAction =
        "Microsoft.ManagedIdentity/userAssignedIdentities/write";
    private const string RoleAssignmentWriteAction =
        "Microsoft.Authorization/roleAssignments/write";
    private const string SecretMetadataAction =
        "Microsoft.KeyVault/vaults/secrets/readMetadata/action";
    private const string KeyMetadataAction =
        "Microsoft.KeyVault/vaults/keys/read";
    private const string CertificateMetadataAction =
        "Microsoft.KeyVault/vaults/certificates/read";
    private const string SecretValueAction =
        "Microsoft.KeyVault/vaults/secrets/getSecret/action";

    private readonly HttpClient _httpClient;
    private readonly Func<DateTimeOffset> _utcNow;

    public AzureAuthorizationEvidenceEvaluator(
        HttpClient httpClient,
        Func<DateTimeOffset>? utcNow = null)
    {
        _httpClient = httpClient;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<WorkloadPermissionAssessment> AssessAsync(
        TokenCredential credential,
        ConnectedIdentity administrator,
        WorkloadIdentityCandidate candidate,
        string keyVaultResourceId,
        CancellationToken cancellationToken)
    {
        var vaultScope = NormalizeResourceId(
            keyVaultResourceId,
            "Microsoft.KeyVault/vaults",
            nameof(keyVaultResourceId));
        var principalId = NormalizeGuid(candidate.PrincipalId, nameof(candidate));
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(
                AzureAuthenticationScopes.ArmApplication.ToArray(),
                tenantId: administrator.HomeTenantId),
            cancellationToken);
        var observedAt = _utcNow();
        var vault = await GetVaultAsync(vaultScope, token.Token, cancellationToken);
        if (!string.Equals(
                vault.TenantId,
                candidate.TenantId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The exact Key Vault belongs to a different tenant than the selected workload identity.");
        }

        var evidence = new List<WorkloadPermissionEvidence>
        {
            new(
                "View identity",
                "Selected administrator",
                WorkloadPermissionEvidenceState.Confirmed,
                candidate.ResourceId,
                "The candidate was returned by the selected administrator's authorized discovery operation.",
                observedAt),
        };

        var isManagedIdentity = IsManagedIdentity(candidate);
        WorkloadPermissionEvidence attachEvidence;
        WorkloadPermissionEvidence identityManagementEvidence;
        if (isManagedIdentity)
        {
            var identityScope = NormalizeResourceId(
                candidate.ResourceId,
                "Microsoft.ManagedIdentity/userAssignedIdentities",
                nameof(candidate));
            var permissions = await TryGetCallerPermissionsAsync(
                identityScope,
                token.Token,
                cancellationToken);
            attachEvidence = ToCallerEvidence(
                "Attach/use identity",
                identityScope,
                ManagedIdentityAttachAction,
                permissions,
                observedAt);
            identityManagementEvidence = ToCallerEvidence(
                "Manage identity",
                identityScope,
                ManagedIdentityWriteAction,
                permissions,
                observedAt);
        }
        else
        {
            attachEvidence = new(
                "Attach/use identity",
                "Selected administrator",
                candidate.IsEnabled
                    ? WorkloadPermissionEvidenceState.Incomplete
                    : WorkloadPermissionEvidenceState.Denied,
                candidate.ResourceId,
                candidate.IsEnabled
                    ? "Service-principal use depends on credential possession and target-service configuration; ARM cannot prove it."
                    : "Microsoft Graph reports this service principal disabled.",
                observedAt);
            identityManagementEvidence = new(
                "Manage identity",
                "Selected administrator",
                WorkloadPermissionEvidenceState.Incomplete,
                candidate.ResourceId,
                "Service-principal management uses Microsoft Graph roles and ownership, not Azure Resource Manager permissions.",
                observedAt);
        }

        evidence.Add(attachEvidence);
        evidence.Add(identityManagementEvidence);

        var vaultCallerPermissions = await TryGetCallerPermissionsAsync(
            vaultScope,
            token.Token,
            cancellationToken);
        var roleManagementEvidence = ToCallerEvidence(
            "Manage role assignments",
            vaultScope,
            RoleAssignmentWriteAction,
            vaultCallerPermissions,
            observedAt);

        WorkloadPermissionEvidence metadataEvidence;
        WorkloadPermissionEvidence secretValueEvidence;
        if (!vault.UsesAzureRbac)
        {
            metadataEvidence = UnsupportedAccessPolicyEvidence(
                "List Key Vault metadata",
                vaultScope,
                observedAt);
            secretValueEvidence = UnsupportedAccessPolicyEvidence(
                "Read secret values",
                vaultScope,
                observedAt);
        }
        else
        {
            var assignments = await GetRoleAssignmentsAsync(
                vaultScope,
                principalId,
                token.Token,
                cancellationToken);
            var definitions = await GetRoleDefinitionsAsync(
                assignments,
                token.Token,
                cancellationToken);
            var denyResult = await TryGetDenyAssignmentsAsync(
                vaultScope,
                token.Token,
                cancellationToken);
            metadataEvidence = EvaluateMetadata(
                assignments,
                definitions,
                denyResult,
                principalId,
                vaultScope,
                observedAt);
            secretValueEvidence = EvaluateAction(
                "Read secret values",
                SecretValueAction,
                assignments,
                definitions,
                denyResult,
                principalId,
                vaultScope,
                observedAt);
        }

        evidence.Add(metadataEvidence);
        evidence.Add(secretValueEvidence);
        evidence.Add(roleManagementEvidence);

        return new WorkloadPermissionAssessment(
            candidate.Permissions.DirectoryVisibility,
            FormatCallerSummary(attachEvidence),
            FormatCallerSummary(identityManagementEvidence),
            FormatKeyVaultSummary(metadataEvidence, secretValueEvidence),
            FormatCallerSummary(roleManagementEvidence))
        {
            Evidence = evidence,
        };
    }

    public async Task<bool> IsCallerDataActionAllowedAsync(
        TokenCredential credential,
        ConnectedIdentity identity,
        string keyVaultResourceId,
        string dataAction,
        CancellationToken cancellationToken)
    {
        var vaultScope = NormalizeResourceId(
            keyVaultResourceId,
            "Microsoft.KeyVault/vaults",
            nameof(keyVaultResourceId));
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(
                AzureAuthenticationScopes.ArmApplication.ToArray(),
                tenantId: identity.HomeTenantId),
            cancellationToken);
        var vault = await GetVaultAsync(
            vaultScope,
            token.Token,
            cancellationToken);
        if (!string.Equals(
                vault.TenantId,
                identity.HomeTenantId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var permissions = await TryGetCallerPermissionsAsync(
            vaultScope,
            token.Token,
            cancellationToken);
        return permissions is not null &&
               permissions.Any(permission =>
                   IsAllowed(permission, dataAction, dataAction: true));
    }

    private async Task<VaultAuthorizationModel> GetVaultAsync(
        string vaultScope,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var uri = CreateArmUri(vaultScope, $"api-version={KeyVaultApiVersion}");
        using var document = await GetDocumentAsync(
            uri,
            bearerToken,
            "Key Vault authorization-model read",
            cancellationToken) ?? throw new InvalidOperationException(
            "Azure Key Vault authorization evidence was unexpectedly unavailable.");
        if (!document.RootElement.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Azure returned an invalid Key Vault resource.");
        }

        var tenantId = RequiredGuidString(properties, "tenantId");
        var usesAzureRbac =
            properties.TryGetProperty("enableRbacAuthorization", out var rbac) &&
            rbac.ValueKind == JsonValueKind.True;
        return new VaultAuthorizationModel(tenantId, usesAzureRbac);
    }

    private async Task<IReadOnlyList<PermissionSet>?> TryGetCallerPermissionsAsync(
        string scope,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var uri = CreateArmUri(
            $"{scope}/providers/Microsoft.Authorization/permissions",
            $"api-version={AuthorizationApiVersion}");
        return await GetPagedAsync(
            uri,
            bearerToken,
            "caller-permissions read",
            ParsePermissionSet,
            allowAuthorizationFailure: true,
            cancellationToken);
    }

    private async Task<IReadOnlyList<RoleAssignment>> GetRoleAssignmentsAsync(
        string vaultScope,
        string principalId,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString($"assignedTo('{principalId}')");
        var uri = CreateArmUri(
            $"{vaultScope}/providers/Microsoft.Authorization/roleAssignments",
            $"api-version={AuthorizationApiVersion}&$filter={filter}");
        return await GetPagedAsync(
                   uri,
                   bearerToken,
                   "role-assignment read",
                   ParseRoleAssignment,
                   allowAuthorizationFailure: false,
                   cancellationToken)
               ?? throw new InvalidOperationException(
                   "Azure role-assignment evidence was unexpectedly unavailable.");
    }

    private async Task<IReadOnlyDictionary<string, RoleDefinition>> GetRoleDefinitionsAsync(
        IReadOnlyList<RoleAssignment> assignments,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var definitionIds = assignments
            .Select(assignment => assignment.RoleDefinitionId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (definitionIds.Length > 100)
        {
            throw new InvalidDataException(
                "Azure returned more role definitions than the safe assessment limit.");
        }

        var definitions = new Dictionary<string, RoleDefinition>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var definitionId in definitionIds)
        {
            var normalizedId = NormalizeRoleDefinitionId(definitionId);
            var uri = CreateArmUri(
                normalizedId,
                $"api-version={AuthorizationApiVersion}");
            using var document = await GetDocumentAsync(
                uri,
                bearerToken,
                "role-definition read",
                cancellationToken) ?? throw new InvalidOperationException(
                "Azure role-definition evidence was unexpectedly unavailable.");
            definitions[normalizedId] = ParseRoleDefinition(document.RootElement);
        }

        return definitions;
    }

    private async Task<DenyAssignmentResult> TryGetDenyAssignmentsAsync(
        string vaultScope,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var filter = Uri.EscapeDataString("atScope()");
        var uri = CreateArmUri(
            $"{vaultScope}/providers/Microsoft.Authorization/denyAssignments",
            $"api-version={AuthorizationApiVersion}&$filter={filter}");
        var assignments = await GetPagedAsync(
            uri,
            bearerToken,
            "deny-assignment read",
            ParseDenyAssignment,
            allowAuthorizationFailure: true,
            cancellationToken);
        return assignments is null
            ? new DenyAssignmentResult([], false)
            : new DenyAssignmentResult(assignments, true);
    }

    private async Task<IReadOnlyList<T>?> GetPagedAsync<T>(
        Uri initialUri,
        string bearerToken,
        string operation,
        Func<JsonElement, T> parser,
        bool allowAuthorizationFailure,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        Uri? pageUri = initialUri;
        for (var page = 0; pageUri is not null && page < MaximumPages; page++)
        {
            EnsureTrustedArmUri(pageUri);
            using var document = await GetDocumentAsync(
                pageUri,
                bearerToken,
                operation,
                cancellationToken,
                allowAuthorizationFailure);
            if (document is null)
                return null;
            if (!document.RootElement.TryGetProperty("value", out var values) ||
                values.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    $"Azure returned an invalid {operation} response.");
            }

            foreach (var value in values.EnumerateArray())
            {
                if (items.Count >= MaximumItems)
                {
                    throw new InvalidDataException(
                        $"Azure {operation} exceeded the safe item limit.");
                }

                items.Add(parser(value));
            }

            pageUri = ReadNextLink(document.RootElement);
        }

        if (pageUri is not null)
        {
            throw new InvalidDataException(
                $"Azure {operation} exceeded the safe page limit.");
        }

        return items;
    }

    private async Task<JsonDocument?> GetDocumentAsync(
        Uri uri,
        string bearerToken,
        string operation,
        CancellationToken cancellationToken,
        bool allowAuthorizationFailure = false)
    {
        EnsureTrustedArmUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (allowAuthorizationFailure &&
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new WorkloadAuthorizationEvidenceException(
                (int)response.StatusCode,
                operation);
        }

        return await BoundedJsonDocument.ReadAsync(
            response.Content,
            $"Azure {operation}",
            cancellationToken);
    }

    private static WorkloadPermissionEvidence EvaluateMetadata(
        IReadOnlyList<RoleAssignment> assignments,
        IReadOnlyDictionary<string, RoleDefinition> definitions,
        DenyAssignmentResult denyResult,
        string principalId,
        string vaultScope,
        DateTimeOffset observedAt)
    {
        var actions = new[]
        {
            SecretMetadataAction,
            KeyMetadataAction,
            CertificateMetadataAction,
        };
        var results = actions
            .Select(action => EvaluateActionState(
                action,
                assignments,
                definitions,
                denyResult,
                principalId,
                vaultScope))
            .ToArray();
        var state = results.All(result =>
                result == WorkloadPermissionEvidenceState.Confirmed)
            ? WorkloadPermissionEvidenceState.Confirmed
            : results.Any(result =>
                result == WorkloadPermissionEvidenceState.Denied)
                ? WorkloadPermissionEvidenceState.Denied
                : results.Any(result =>
                    result == WorkloadPermissionEvidenceState.Incomplete)
                    ? WorkloadPermissionEvidenceState.Incomplete
                    : results.Any(result =>
                        result == WorkloadPermissionEvidenceState.Conditional)
                        ? WorkloadPermissionEvidenceState.Conditional
                        : WorkloadPermissionEvidenceState.NotGranted;
        return new WorkloadPermissionEvidence(
            "List Key Vault metadata",
            "Selected workload identity",
            state,
            vaultScope,
            $"Static Azure RBAC evidence for secret, key, and certificate metadata actions: {string.Join(", ", results)}. No data-plane operation was performed.",
            observedAt);
    }

    private static WorkloadPermissionEvidence EvaluateAction(
        string capability,
        string action,
        IReadOnlyList<RoleAssignment> assignments,
        IReadOnlyDictionary<string, RoleDefinition> definitions,
        DenyAssignmentResult denyResult,
        string principalId,
        string vaultScope,
        DateTimeOffset observedAt)
    {
        var state = EvaluateActionState(
            action,
            assignments,
            definitions,
            denyResult,
            principalId,
            vaultScope);
        return new WorkloadPermissionEvidence(
            capability,
            "Selected workload identity",
            state,
            vaultScope,
            state switch
            {
                WorkloadPermissionEvidenceState.Confirmed =>
                    "Applicable unconditional role evidence grants the action and no applicable deny was observed. No data-plane operation was performed.",
                WorkloadPermissionEvidenceState.Denied =>
                    "An applicable unconditional deny assignment blocks the action.",
                WorkloadPermissionEvidenceState.Conditional =>
                    "A matching grant or deny has an Azure condition that cannot be evaluated without a concrete request.",
                WorkloadPermissionEvidenceState.Incomplete =>
                    "Deny-assignment visibility or group applicability is incomplete, so an allow cannot be proven.",
                _ =>
                    "No applicable role definition in the observable authorization graph grants the action.",
            },
            observedAt);
    }

    private static WorkloadPermissionEvidenceState EvaluateActionState(
        string action,
        IReadOnlyList<RoleAssignment> assignments,
        IReadOnlyDictionary<string, RoleDefinition> definitions,
        DenyAssignmentResult denyResult,
        string principalId,
        string vaultScope)
    {
        var hasUnconditionalGrant = false;
        var hasConditionalGrant = false;
        foreach (var assignment in assignments)
        {
            if (!IsScopeAtOrAbove(assignment.Scope, vaultScope) ||
                !definitions.TryGetValue(
                    NormalizeRoleDefinitionId(assignment.RoleDefinitionId),
                    out var definition))
            {
                continue;
            }

            var matchingPermissions = definition.Permissions
                .Where(permission =>
                    IsAllowed(permission, action, dataAction: true))
                .ToArray();
            if (matchingPermissions.Length == 0)
                continue;

            if (string.IsNullOrWhiteSpace(assignment.Condition) &&
                matchingPermissions.Any(permission =>
                    string.IsNullOrWhiteSpace(permission.Condition)))
            {
                hasUnconditionalGrant = true;
            }
            else
            {
                hasConditionalGrant = true;
            }
        }

        var hasConditionalDeny = false;
        var hasAmbiguousGroupDeny = false;
        if (!denyResult.IsComplete)
            hasAmbiguousGroupDeny = true;
        foreach (var deny in denyResult.Assignments)
        {
            if (!DenyScopeApplies(deny, vaultScope) ||
                !deny.Permissions.Any(permission =>
                    IsDenied(permission, action, dataAction: true)))
            {
                continue;
            }

            var relationship = DenyPrincipalRelationship(deny, principalId);
            if (relationship == PrincipalRelationship.DoesNotApply)
                continue;
            if (relationship == PrincipalRelationship.MayApply)
            {
                hasAmbiguousGroupDeny = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(deny.Condition) &&
                deny.Permissions.All(permission =>
                    string.IsNullOrWhiteSpace(permission.Condition)))
            {
                return WorkloadPermissionEvidenceState.Denied;
            }

            hasConditionalDeny = true;
        }

        if (hasAmbiguousGroupDeny)
            return WorkloadPermissionEvidenceState.Incomplete;
        if (hasConditionalDeny)
            return WorkloadPermissionEvidenceState.Conditional;
        if (hasUnconditionalGrant)
            return WorkloadPermissionEvidenceState.Confirmed;
        return hasConditionalGrant
            ? WorkloadPermissionEvidenceState.Conditional
            : WorkloadPermissionEvidenceState.NotGranted;
    }

    private static WorkloadPermissionEvidence ToCallerEvidence(
        string capability,
        string scope,
        string action,
        IReadOnlyList<PermissionSet>? permissions,
        DateTimeOffset observedAt)
    {
        var state = permissions is null
            ? WorkloadPermissionEvidenceState.Incomplete
            : permissions.Any(permission =>
                IsAllowed(permission, action, dataAction: false))
                ? WorkloadPermissionEvidenceState.Confirmed
                : WorkloadPermissionEvidenceState.NotGranted;
        return new WorkloadPermissionEvidence(
            capability,
            "Selected administrator",
            state,
            scope,
            state switch
            {
                WorkloadPermissionEvidenceState.Confirmed =>
                    "Azure's caller-permissions endpoint includes the required action at this exact resource.",
                WorkloadPermissionEvidenceState.NotGranted =>
                    "Azure's caller-permissions endpoint does not include the required action at this exact resource.",
                _ =>
                    "Azure did not authorize reading the selected caller's effective permissions at this resource.",
            },
            observedAt);
    }

    private static WorkloadPermissionEvidence UnsupportedAccessPolicyEvidence(
        string capability,
        string scope,
        DateTimeOffset observedAt) =>
        new(
            capability,
            "Selected workload identity",
            WorkloadPermissionEvidenceState.Incomplete,
            scope,
            "This Key Vault uses access policies rather than Azure RBAC. Connect the identity and perform an explicit runtime operation to observe data-plane access.",
            observedAt);

    private static string FormatCallerSummary(
        WorkloadPermissionEvidence evidence) =>
        evidence.State switch
        {
            WorkloadPermissionEvidenceState.Confirmed =>
                $"Confirmed for the selected administrator at {evidence.Scope}.",
            WorkloadPermissionEvidenceState.Denied =>
                $"Unavailable at {evidence.Scope}. {evidence.Basis}",
            WorkloadPermissionEvidenceState.NotGranted =>
                $"Not granted to the selected administrator at {evidence.Scope}.",
            WorkloadPermissionEvidenceState.NotApplicable =>
                $"Not applicable. {evidence.Basis}",
            _ => $"Not proven at {evidence.Scope}. {evidence.Basis}",
        };

    private static string FormatKeyVaultSummary(
        WorkloadPermissionEvidence metadata,
        WorkloadPermissionEvidence secretValue) =>
        $"Metadata: {metadata.State}; secret values: {secretValue.State}. " +
        "This is read-only authorization evidence, not a runtime data-plane test.";

    private static PermissionSet ParsePermissionSet(JsonElement element) =>
        new(
            ReadStrings(element, "actions"),
            ReadStrings(element, "notActions"),
            ReadStrings(element, "dataActions"),
            ReadStrings(element, "notDataActions"),
            OptionalString(element, "condition"));

    private static RoleAssignment ParseRoleAssignment(JsonElement element)
    {
        var properties = RequiredObject(element, "properties");
        return new RoleAssignment(
            NormalizeRoleDefinitionId(
                RequiredString(properties, "roleDefinitionId")),
            RequiredString(properties, "scope"),
            OptionalString(properties, "condition"));
    }

    private static RoleDefinition ParseRoleDefinition(JsonElement element)
    {
        var properties = RequiredObject(element, "properties");
        if (!properties.TryGetProperty("permissions", out var permissions) ||
            permissions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Azure returned a role definition without a permission array.");
        }

        return new RoleDefinition(
            OptionalString(properties, "roleName"),
            permissions.EnumerateArray().Select(ParsePermissionSet).ToArray());
    }

    private static DenyAssignment ParseDenyAssignment(JsonElement element)
    {
        var properties = RequiredObject(element, "properties");
        if (!properties.TryGetProperty("permissions", out var permissions) ||
            permissions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Azure returned a deny assignment without a permission array.");
        }

        return new DenyAssignment(
            RequiredString(properties, "scope"),
            properties.TryGetProperty("doNotApplyToChildScopes", out var noChildren) &&
            noChildren.ValueKind == JsonValueKind.True,
            ReadPrincipals(properties, "principals"),
            ReadPrincipals(properties, "excludePrincipals"),
            permissions.EnumerateArray().Select(ParsePermissionSet).ToArray(),
            OptionalString(properties, "condition"));
    }

    private static AuthorizationPrincipal[] ReadPrincipals(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values) ||
            values.ValueKind == JsonValueKind.Null)
            return [];
        if (values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                $"Azure returned an invalid {propertyName} array.");
        return values.EnumerateArray()
            .Select(value => new AuthorizationPrincipal(
                RequiredString(value, "id"),
                OptionalString(value, "type")))
            .ToArray();
    }

    private static List<string> ReadStrings(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values) ||
            values.ValueKind == JsonValueKind.Null)
            return [];
        if (values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                $"Azure returned an invalid {propertyName} array.");
        var result = new List<string>();
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new InvalidDataException(
                    $"Azure returned an invalid {propertyName} value.");
            }

            result.Add(value.GetString()!);
        }

        return result;
    }

    private static bool IsAllowed(
        PermissionSet permission,
        string action,
        bool dataAction)
    {
        var includes = dataAction ? permission.DataActions : permission.Actions;
        var excludes =
            dataAction ? permission.NotDataActions : permission.NotActions;
        return includes.Any(pattern => MatchesAction(pattern, action)) &&
               !excludes.Any(pattern => MatchesAction(pattern, action));
    }

    private static bool IsDenied(
        PermissionSet permission,
        string action,
        bool dataAction)
    {
        var includes = dataAction ? permission.DataActions : permission.Actions;
        var excludes =
            dataAction ? permission.NotDataActions : permission.NotActions;
        return includes.Any(pattern => MatchesAction(pattern, action)) &&
               !excludes.Any(pattern => MatchesAction(pattern, action));
    }

    private static bool MatchesAction(string pattern, string action)
    {
        var parts = pattern.Split('*');
        if (parts.Length == 1)
            return string.Equals(pattern, action, StringComparison.OrdinalIgnoreCase);

        var position = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0)
                continue;
            var match = action.IndexOf(
                part,
                position,
                StringComparison.OrdinalIgnoreCase);
            if (match < 0 ||
                index == 0 && match != 0)
                return false;
            position = match + part.Length;
        }

        return pattern.EndsWith('*') ||
               position == action.Length;
    }

    private static PrincipalRelationship DenyPrincipalRelationship(
        DenyAssignment assignment,
        string principalId)
    {
        if (assignment.ExcludePrincipals.Any(principal =>
                IsPrincipal(principal, principalId) ||
                IsAllPrincipals(principal)))
        {
            return PrincipalRelationship.DoesNotApply;
        }

        var directOrAll = assignment.Principals.Any(principal =>
            IsPrincipal(principal, principalId) ||
            IsAllPrincipals(principal));
        var hasGroupPrincipal = assignment.Principals.Any(
            principal => IsGroup(principal.Type));
        var hasGroupExclusion = assignment.ExcludePrincipals.Any(
            principal => IsGroup(principal.Type));
        if (directOrAll)
            return hasGroupExclusion
                ? PrincipalRelationship.MayApply
                : PrincipalRelationship.Applies;
        return hasGroupPrincipal
            ? PrincipalRelationship.MayApply
            : PrincipalRelationship.DoesNotApply;
    }

    private static bool IsPrincipal(
        AuthorizationPrincipal principal,
        string principalId) =>
        string.Equals(principal.Id, principalId, StringComparison.OrdinalIgnoreCase);

    private static bool IsAllPrincipals(AuthorizationPrincipal principal) =>
        string.Equals(
            principal.Id,
            AllPrincipalsId,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsGroup(string type) =>
        string.Equals(type, "Group", StringComparison.OrdinalIgnoreCase);

    private static bool DenyScopeApplies(
        DenyAssignment assignment,
        string targetScope)
    {
        if (!IsScopeAtOrAbove(assignment.Scope, targetScope))
            return false;
        return !assignment.DoNotApplyToChildScopes ||
               string.Equals(
                   NormalizeScope(assignment.Scope),
                   NormalizeScope(targetScope),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScopeAtOrAbove(
        string assignmentScope,
        string targetScope)
    {
        var ancestor = NormalizeScope(assignmentScope);
        var target = NormalizeScope(targetScope);
        return string.Equals(
                   ancestor,
                   target,
                   StringComparison.OrdinalIgnoreCase) ||
               target.StartsWith(
                   $"{ancestor}/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeScope(string scope)
    {
        var value = scope.Trim().TrimEnd('/');
        if (!value.StartsWith('/') ||
            value.Contains('?') ||
            value.Contains('#'))
        {
            throw new InvalidDataException(
                "Azure returned an invalid authorization scope.");
        }

        return value;
    }

    private static string NormalizeRoleDefinitionId(string value)
    {
        var normalized = NormalizeScope(value);
        const string marker =
            "/providers/Microsoft.Authorization/roleDefinitions/";
        var markerIndex = normalized.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0 ||
            !Guid.TryParse(normalized[(markerIndex + marker.Length)..], out _))
        {
            throw new InvalidDataException(
                "Azure returned an invalid role-definition resource ID.");
        }

        return normalized;
    }

    private static string NormalizeResourceId(
        string value,
        string expectedResourceType,
        string parameterName)
    {
        ResourceIdentifier resourceId;
        try
        {
            resourceId = new ResourceIdentifier(value.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException)
        {
            throw new ArgumentException(
                "A valid Azure resource ID is required.",
                parameterName,
                exception);
        }

        if (resourceId.SubscriptionId is null ||
            !string.Equals(
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

    private static string NormalizeGuid(string value, string parameterName)
    {
        if (!Guid.TryParse(value, out var parsed))
            throw new ArgumentException("A GUID is required.", parameterName);
        return parsed.ToString("D");
    }

    private static bool IsManagedIdentity(WorkloadIdentityCandidate candidate) =>
        candidate.IdentityType.StartsWith(
            "User-assigned managed identity",
            StringComparison.OrdinalIgnoreCase);

    private static JsonElement RequiredObject(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Azure omitted required object {propertyName}.");
        }

        return value;
    }

    private static string RequiredString(
        JsonElement element,
        string propertyName)
    {
        var value = OptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Azure omitted required field {propertyName}.");
        return value;
    }

    private static string RequiredGuidString(
        JsonElement element,
        string propertyName)
    {
        var value = RequiredString(element, propertyName);
        if (!Guid.TryParse(value, out var parsed))
            throw new InvalidDataException(
                $"Azure returned an invalid {propertyName} identifier.");
        return parsed.ToString("D");
    }

    private static string OptionalString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static Uri? ReadNextLink(JsonElement root)
    {
        if (!root.TryGetProperty("nextLink", out var next) ||
            next.ValueKind == JsonValueKind.Null)
            return null;
        if (next.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(next.GetString(), UriKind.Absolute, out var nextUri))
        {
            throw new InvalidDataException(
                "Azure returned an invalid authorization next-page link.");
        }

        EnsureTrustedArmUri(nextUri);
        return nextUri;
    }

    private static Uri CreateArmUri(string path, string query)
    {
        var normalizedPath = NormalizeScope(path);
        return new Uri(
            $"https://management.azure.com{normalizedPath}?{query}",
            UriKind.Absolute);
    }

    private static void EnsureTrustedArmUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                uri.Host,
                "management.azure.com",
                StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort)
        {
            throw new InvalidDataException(
                "Azure returned an untrusted authorization endpoint.");
        }
    }

    private sealed record VaultAuthorizationModel(
        string TenantId,
        bool UsesAzureRbac);

    private sealed record PermissionSet(
        IReadOnlyList<string> Actions,
        IReadOnlyList<string> NotActions,
        IReadOnlyList<string> DataActions,
        IReadOnlyList<string> NotDataActions,
        string Condition);

    private sealed record RoleAssignment(
        string RoleDefinitionId,
        string Scope,
        string Condition);

    private sealed record RoleDefinition(
        string Name,
        IReadOnlyList<PermissionSet> Permissions);

    private sealed record DenyAssignment(
        string Scope,
        bool DoNotApplyToChildScopes,
        IReadOnlyList<AuthorizationPrincipal> Principals,
        IReadOnlyList<AuthorizationPrincipal> ExcludePrincipals,
        IReadOnlyList<PermissionSet> Permissions,
        string Condition);

    private sealed record AuthorizationPrincipal(
        string Id,
        string Type);

    private sealed record DenyAssignmentResult(
        IReadOnlyList<DenyAssignment> Assignments,
        bool IsComplete);

    private enum PrincipalRelationship
    {
        DoesNotApply,
        Applies,
        MayApply,
    }
}
