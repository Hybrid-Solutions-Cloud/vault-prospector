using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.Azure;

namespace VaultProspector.Providers.Azure.Tests;

public sealed class AuthenticationConfigurationTests
{
    [Fact]
    public void SynchronizationCredentialContractHasNoInteractiveAcquisitionPath()
    {
        var method = Assert.Single(
            typeof(IAzureCredentialProvider).GetMethods());

        Assert.Equal("GetCredentialAsync", method.Name);
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(bool));
    }

    [Fact]
    public void InteractiveSignInUsesOneResourceAudience()
    {
        Assert.Equal([AzureAuthenticationScopes.ArmDelegated], AzureAuthenticationScopes.InteractiveSignIn);
        Assert.DoesNotContain(AzureAuthenticationScopes.KeyVaultDelegated, AzureAuthenticationScopes.InteractiveSignIn);
        Assert.Equal([AzureAuthenticationScopes.KeyVaultDelegated], AzureAuthenticationScopes.AdditionalConsent);
        Assert.Equal(["https://management.azure.com/.default"], AzureAuthenticationScopes.ArmApplication);
    }

    [Fact]
    public async Task CredentialAcquisitionUsesIdentityClientAndFailsForMissingAccount()
    {
        var provider = new MsalIdentityProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var identity = new ConnectedIdentity(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);

        if (!OperatingSystem.IsWindows())
        {
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));
            return;
        }

        await Assert.ThrowsAsync<MsalUiRequiredException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidStoredClientIdIsRejectedBeforeCachePathConstruction()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-msal-{Guid.NewGuid():N}");
        var provider = new MsalIdentityProvider(directory);
        var identity = new ConnectedIdentity(Guid.NewGuid(), "../../outside-cache", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetCredentialAsync(identity, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task RemovingWorkloadProfileDoesNotCreateOrOpenHumanTokenCache()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-workload-remove-{Guid.NewGuid():N}");
        var provider = new MsalIdentityProvider(directory);
        var identity = new ConnectedIdentity(
            Guid.NewGuid(),
            string.Empty,
            "workload",
            string.Empty,
            "Azure host",
            string.Empty,
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow,
            true,
            IdentityType.ManagedIdentity);

        await provider.RemoveAsync(identity, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task FederatedProfileUsesWorkloadCredentialWithoutHumanTokenCache()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-federated-{Guid.NewGuid():N}");
        var tokenPath = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-federated-{Guid.NewGuid():N}.token");
        await File.WriteAllTextAsync(
            tokenPath,
            "test-token-content",
            TestContext.Current.CancellationToken);
        try
        {
            var provider = new MsalIdentityProvider(directory);
            var identity = new ConnectedIdentity(
                Guid.NewGuid(),
                "11111111-1111-1111-1111-111111111111",
                "workload",
                string.Empty,
                "Federated automation",
                "22222222-2222-2222-2222-222222222222",
                AuthenticationState.Ready,
                DateTimeOffset.UtcNow,
                true,
                IdentityType.FederatedServicePrincipal,
                tokenPath);

            var credential = await provider.GetCredentialAsync(
                identity,
                TestContext.Current.CancellationToken);

            Assert.IsType<WorkloadIdentityCredential>(credential);
            Assert.False(Directory.Exists(directory));
        }
        finally
        {
            File.Delete(tokenPath);
        }
    }

    [Fact]
    public async Task HostManagedIdentityEndpointEnablesOptionWithoutImdsProbe()
    {
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError);
        var detector = new ManagedIdentityEnvironmentDetector(
            new HttpClient(handler),
            name => name == "IDENTITY_ENDPOINT" ? "http://localhost/identity" : null);

        var result = await detector.DetectAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSupported);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task AzureInstanceMetadataEnablesManagedIdentityOption()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var detector = new ManagedIdentityEnvironmentDetector(
            new HttpClient(handler),
            _ => null);

        var result = await detector.DetectAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSupported);
        Assert.Equal(1, handler.Calls);
        Assert.True(handler.SawMetadataHeader);
    }

    [Fact]
    public async Task OrdinaryHostDoesNotOfferManagedIdentity()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var detector = new ManagedIdentityEnvironmentDetector(
            new HttpClient(handler),
            _ => null);

        var result = await detector.DetectAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSupported);
        Assert.Contains("unavailable", result.SafeReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManagedIdentityDryRunNamesExactScopeWithoutPerformingMutation()
    {
        var plan = WorkloadIdentityDiscoveryService.BuildManagedIdentityDryRun(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "rg-automation",
            "vault-prospector-reader");

        Assert.False(plan.PerformsMutations);
        var operation = Assert.Single(plan.Operations);
        Assert.Equal("Create", operation.Operation);
        Assert.Equal(
            "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg-automation/providers/Microsoft.ManagedIdentity/userAssignedIdentities/vault-prospector-reader",
            operation.Scope);
        Assert.Contains("without attaching", operation.ExpectedEffect, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without", operation.ExpectedEffect, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManagedIdentityDryRunKeepsRoleAssignmentAtExactVaultScope()
    {
        const string vaultScope =
            "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg-vault/providers/Microsoft.KeyVault/vaults/example";
        const string roleDefinition =
            "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/providers/Microsoft.Authorization/roleDefinitions/cccccccc-cccc-cccc-cccc-cccccccccccc";

        var plan = WorkloadIdentityDiscoveryService.BuildManagedIdentityDryRun(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "rg-automation",
            "vault-prospector-reader",
            vaultScope,
            roleDefinition);

        Assert.False(plan.PerformsMutations);
        Assert.Equal(2, plan.Operations.Count);
        Assert.Equal(vaultScope, plan.Operations[1].Scope);
        Assert.Contains(roleDefinition, plan.Operations[1].ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-guid", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "rg", "identity")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "not-a-guid", "rg", "identity")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "../rg", "identity")]
    public void ManagedIdentityDryRunRejectsAmbiguousOrInvalidScope(
        string tenantId,
        string subscriptionId,
        string resourceGroup,
        string identityName)
    {
        Assert.Throws<ArgumentException>(() =>
            WorkloadIdentityDiscoveryService.BuildManagedIdentityDryRun(
                tenantId,
                subscriptionId,
                resourceGroup,
                identityName));
    }

    [Fact]
    public async Task GraphDiscoveryPaginatesOnlyTrustedEndpointAndDistinguishesPermissions()
    {
        var handler = new GraphSequenceHandler(
            """
            {
              "value": [
                {
                  "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "appId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                  "displayName": "Automation Reader",
                  "servicePrincipalType": "Application",
                  "accountEnabled": true,
                  "appOwnerOrganizationId": "22222222-2222-2222-2222-222222222222"
                }
              ],
              "@odata.nextLink": "https://graph.microsoft.com/v1.0/servicePrincipals?$skiptoken=next"
            }
            """,
            """
            {
              "value": [
                {
                  "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
                  "appId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
                  "displayName": "Disabled Automation",
                  "servicePrincipalType": "Application",
                  "accountEnabled": false,
                  "appOwnerOrganizationId": "22222222-2222-2222-2222-222222222222"
                }
              ]
            }
            """);
        var credential = new StaticTokenCredential();
        var service = new WorkloadIdentityDiscoveryService(
            credential,
            new HttpClient(handler));

        var candidates = await service.ListServicePrincipalsAsync(
            InteractiveAdministrator(),
            TestContext.Current.CancellationToken);

        var candidate = Assert.Single(candidates);
        Assert.Equal(2, handler.Calls);
        Assert.All(handler.AuthorizationSchemes, scheme => Assert.Equal("Bearer", scheme));
        Assert.Equal(AzureAuthenticationScopes.GraphDirectoryRead, credential.LastScopes);
        Assert.Equal(
            "22222222-2222-2222-2222-222222222222",
            credential.LastTenantId);
        Assert.Contains("Confirmed", candidate.Permissions.DirectoryVisibility, StringComparison.Ordinal);
        Assert.Contains("Customer-owned", candidate.Permissions.IdentityManagement, StringComparison.Ordinal);
        Assert.True(candidate.IsEnabled);
    }

    [Fact]
    public async Task GraphDiscoveryExcludesMicrosoftFirstPartyServicePrincipalsByDefault()
    {
        var handler = new GraphSequenceHandler(
            """
            {
              "value": [
                {
                  "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                  "appId": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                  "displayName": "Microsoft Infrastructure",
                  "servicePrincipalType": "Application",
                  "accountEnabled": true,
                  "appOwnerOrganizationId": "72f988bf-86f1-41af-91ab-2d7cd011db47"
                },
                {
                  "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
                  "appId": "dddddddd-dddd-dddd-dddd-dddddddddddd",
                  "displayName": "Customer Automation",
                  "servicePrincipalType": "Application",
                  "accountEnabled": true,
                  "appOwnerOrganizationId": "22222222-2222-2222-2222-222222222222"
                },
                {
                  "id": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
                  "appId": "ffffffff-ffff-ffff-ffff-ffffffffffff",
                  "displayName": "External enterprise application",
                  "servicePrincipalType": "Application",
                  "accountEnabled": true,
                  "appOwnerOrganizationId": "99999999-9999-9999-9999-999999999999"
                },
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "appId": "22222222-2222-2222-2222-222222222222",
                  "displayName": "Managed identity service principal",
                  "servicePrincipalType": "ManagedIdentity",
                  "accountEnabled": true,
                  "appOwnerOrganizationId": "22222222-2222-2222-2222-222222222222"
                }
              ]
            }
            """);
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            new HttpClient(handler));

        var candidates = await service.ListServicePrincipalsAsync(
            InteractiveAdministrator(),
            TestContext.Current.CancellationToken);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Customer Automation", candidate.DisplayName);
    }

    [Theory]
    [InlineData("https://attacker.invalid/collect")]
    [InlineData("https://graph.microsoft.com:444/v1.0/servicePrincipals")]
    public async Task GraphDiscoveryRejectsUntrustedPaginationBeforeSendingToken(
        string nextLink)
    {
        var handler = new GraphSequenceHandler(
            $$"""
            {
              "value": [],
              "@odata.nextLink": "{{nextLink}}"
            }
            """);
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ListServicePrincipalsAsync(
                InteractiveAdministrator(),
                TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GraphDiscoveryRejectsOversizedResponseBeforeJsonParsing()
    {
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            new HttpClient(new OversizedJsonHandler()));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ListServicePrincipalsAsync(
                InteractiveAdministrator(),
                TestContext.Current.CancellationToken));

        Assert.Contains(
            "response-size limit",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnterprisePolicyDeniesWorkloadDiscoveryAndPlansBeforeNetwork()
    {
        var handler = new GraphSequenceHandler("""{ "value": [] }""");
        var credential = new StaticTokenCredential();
        IWorkloadIdentityAdministrationService service =
            new WorkloadIdentityDiscoveryService(
                credential,
                new HttpClient(handler),
                enterprisePolicy: new FixedEnterprisePolicy(
                    new EnterprisePolicySnapshot(
                        true,
                        allowedTenantIds:
                            ["33333333-3333-3333-3333-333333333333"])));

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.ListServicePrincipalsAsync(
                InteractiveAdministrator(),
                TestContext.Current.CancellationToken));
        Assert.Throws<EnterprisePolicyDeniedException>(
            () => service.BuildServicePrincipalDryRun(
                "22222222-2222-2222-2222-222222222222",
                "automation"));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task EnterprisePolicyDeniesWorkloadAssessmentByCandidateTypeBeforeNetwork()
    {
        var handler = new AuthorizationEvidenceHandler();
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler),
            enterprisePolicy: new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds:
                        ["22222222-2222-2222-2222-222222222222"],
                    allowedIdentityTypes:
                        [
                            IdentityType.InteractiveUser,
                            IdentityType.ServicePrincipal,
                        ])));

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.AssessPermissionsAsync(
                InteractiveAdministrator(),
                ManagedIdentityCandidate(),
                VaultScope,
                TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PermissionAssessmentUsesExactCallerAndCandidateEvidenceWithoutMutations()
    {
        var handler = new AuthorizationEvidenceHandler();
        var credential = new StaticTokenCredential();
        var service = new WorkloadIdentityDiscoveryService(
            credential,
            authorizationClient: new HttpClient(handler));

        var result = await service.AssessPermissionsAsync(
            InteractiveAdministrator(),
            ManagedIdentityCandidate(),
            VaultScope,
            TestContext.Current.CancellationToken);

        Assert.Contains("Confirmed", result.Permissions.AttachOrUse, StringComparison.Ordinal);
        Assert.Contains("Not granted", result.Permissions.IdentityManagement, StringComparison.Ordinal);
        Assert.Contains("Metadata: Confirmed", result.Permissions.KeyVaultDataAccess, StringComparison.Ordinal);
        Assert.Contains("secret values: Confirmed", result.Permissions.KeyVaultDataAccess, StringComparison.Ordinal);
        Assert.Contains("Confirmed", result.Permissions.RoleAssignmentManagement, StringComparison.Ordinal);
        Assert.Equal(6, result.Permissions.Evidence.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("management.azure.com", request.Uri.Host);
            Assert.Equal("Bearer", request.AuthorizationScheme);
        });
        Assert.Equal(AzureAuthenticationScopes.ArmApplication, credential.LastScopes);
        Assert.Equal("22222222-2222-2222-2222-222222222222", credential.LastTenantId);
    }

    [Fact]
    public async Task PermissionAssessmentMakesApplicableDenyOverrideInheritedGrant()
    {
        var handler = new AuthorizationEvidenceHandler(
            denyResponse: $$"""
            {
              "value": [
                {
                  "properties": {
                    "scope": "/subscriptions/{{SubscriptionId}}",
                    "doNotApplyToChildScopes": false,
                    "principals": [
                      { "id": "{{PrincipalId}}", "type": "ServicePrincipal" }
                    ],
                    "excludePrincipals": [],
                    "permissions": [
                      {
                        "actions": [],
                        "notActions": [],
                        "dataActions": [ "Microsoft.KeyVault/vaults/secrets/getSecret/action" ],
                        "notDataActions": []
                      }
                    ]
                  }
                }
              ]
            }
            """);
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler));

        var result = await service.AssessPermissionsAsync(
            InteractiveAdministrator(),
            ManagedIdentityCandidate(),
            VaultScope,
            TestContext.Current.CancellationToken);

        var secretEvidence = Assert.Single(
            result.Permissions.Evidence,
            evidence => evidence.Capability == "Read secret values");
        Assert.Equal(WorkloadPermissionEvidenceState.Denied, secretEvidence.State);
        Assert.Contains("secret values: Denied", result.Permissions.KeyVaultDataAccess, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PermissionAssessmentDoesNotPromoteConditionalGrantToAllowed()
    {
        var handler = new AuthorizationEvidenceHandler(
            roleAssignmentCondition:
                "@Resource[Microsoft.KeyVault/vaults:Name] StringEqualsIgnoreCase 'example'");
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler));

        var result = await service.AssessPermissionsAsync(
            InteractiveAdministrator(),
            ManagedIdentityCandidate(),
            VaultScope,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            WorkloadPermissionEvidenceState.Conditional,
            Assert.Single(
                result.Permissions.Evidence,
                evidence => evidence.Capability == "List Key Vault metadata").State);
        Assert.Equal(
            WorkloadPermissionEvidenceState.Conditional,
            Assert.Single(
                result.Permissions.Evidence,
                evidence => evidence.Capability == "Read secret values").State);
    }

    [Fact]
    public async Task PermissionAssessmentFailsClosedWhenDenyAssignmentsAreUnreadable()
    {
        var handler = new AuthorizationEvidenceHandler(
            denyStatusCode: HttpStatusCode.Forbidden);
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler));

        var result = await service.AssessPermissionsAsync(
            InteractiveAdministrator(),
            ManagedIdentityCandidate(),
            VaultScope,
            TestContext.Current.CancellationToken);

        Assert.All(
            result.Permissions.Evidence.Where(evidence =>
                evidence.Subject == "Selected workload identity"),
            evidence => Assert.Equal(
                WorkloadPermissionEvidenceState.Incomplete,
                evidence.State));
        Assert.DoesNotContain(
            "Metadata: Confirmed",
            result.Permissions.KeyVaultDataAccess,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PermissionAssessmentRejectsUntrustedArmPaginationBeforeSendingToken()
    {
        var handler = new AuthorizationEvidenceHandler(
            roleAssignmentNextLink: "https://attacker.invalid/collect");
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.AssessPermissionsAsync(
                InteractiveAdministrator(),
                ManagedIdentityCandidate(),
                VaultScope,
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            handler.Requests,
            request => request.Uri.Host == "attacker.invalid");
    }

    [Fact]
    public async Task PermissionAssessmentDoesNotInterpretKeyVaultAccessPoliciesAsRbac()
    {
        var handler = new AuthorizationEvidenceHandler(usesAzureRbac: false);
        var service = new WorkloadIdentityDiscoveryService(
            new StaticTokenCredential(),
            authorizationClient: new HttpClient(handler));

        var result = await service.AssessPermissionsAsync(
            InteractiveAdministrator(),
            ManagedIdentityCandidate(),
            VaultScope,
            TestContext.Current.CancellationToken);

        Assert.All(
            result.Permissions.Evidence.Where(evidence =>
                evidence.Subject == "Selected workload identity"),
            evidence => Assert.Equal(
                WorkloadPermissionEvidenceState.Incomplete,
                evidence.State));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Uri.AbsolutePath.EndsWith(
                "/roleAssignments",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ServicePrincipalDryRunIsExactAndNonMutating()
    {
        const string vaultScope =
            "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg-vault/providers/Microsoft.KeyVault/vaults/example";
        const string roleDefinition =
            "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/providers/Microsoft.Authorization/roleDefinitions/cccccccc-cccc-cccc-cccc-cccccccccccc";

        var plan = WorkloadIdentityDiscoveryService.BuildServicePrincipalDryRun(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "vault-prospector-reader",
            vaultScope,
            roleDefinition);

        Assert.False(plan.PerformsMutations);
        Assert.Equal("ServicePrincipal", plan.IdentityType);
        Assert.Equal("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", plan.SubscriptionId);
        Assert.Equal("rg-vault", plan.ResourceGroup);
        Assert.Equal(3, plan.Operations.Count);
        Assert.Equal(vaultScope, plan.Operations[2].Scope);
        Assert.DoesNotContain(
            "canary-client-secret",
            string.Join("|", plan.Operations.Select(operation => operation.ExpectedEffect)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/not-a-vault",
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/providers/Microsoft.Authorization/roleDefinitions/cccccccc-cccc-cccc-cccc-cccccccccccc")]
    [InlineData(
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/example",
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/providers/Microsoft.Authorization/locks/cccccccc-cccc-cccc-cccc-cccccccccccc")]
    [InlineData(
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/example",
        "/subscriptions/dddddddd-dddd-dddd-dddd-dddddddddddd/providers/Microsoft.Authorization/roleDefinitions/cccccccc-cccc-cccc-cccc-cccccccccccc")]
    public void DryRunRejectsWrongOrCrossSubscriptionRoleScope(
        string vaultResourceId,
        string roleDefinitionId)
    {
        Assert.Throws<ArgumentException>(() =>
            WorkloadIdentityDiscoveryService.BuildServicePrincipalDryRun(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "vault-prospector-reader",
                vaultResourceId,
                roleDefinitionId));
    }

    private static ConnectedIdentity InteractiveAdministrator() => new(
        Guid.NewGuid(),
        "11111111-1111-1111-1111-111111111111",
        "account",
        "admin@example.invalid",
        "Administrator",
        "22222222-2222-2222-2222-222222222222",
        AuthenticationState.Ready,
        DateTimeOffset.UtcNow);

    private const string SubscriptionId =
        "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string PrincipalId =
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string VaultScope =
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg-vault/providers/Microsoft.KeyVault/vaults/example";
    private const string IdentityScope =
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/resourceGroups/rg-automation/providers/Microsoft.ManagedIdentity/userAssignedIdentities/reader";
    private const string RoleDefinitionId =
        "/subscriptions/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/providers/Microsoft.Authorization/roleDefinitions/cccccccc-cccc-cccc-cccc-cccccccccccc";

    private static WorkloadIdentityCandidate ManagedIdentityCandidate() => new(
        "User-assigned managed identity",
        "22222222-2222-2222-2222-222222222222",
        SubscriptionId,
        "rg-automation",
        "reader",
        IdentityScope,
        "dddddddd-dddd-dddd-dddd-dddddddddddd",
        PrincipalId,
        "eastus",
        true,
        new WorkloadPermissionAssessment(
            "Confirmed",
            "Not proven",
            "Not proven",
            "Not proven",
            "Not proven"));

    private sealed class StaticTokenCredential : TokenCredential
    {
        public IReadOnlyList<string> LastScopes { get; private set; } = [];
        public string? LastTenantId { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            LastScopes = requestContext.Scopes;
            LastTenantId = requestContext.TenantId;
            return new("test-access-token", DateTimeOffset.UtcNow.AddMinutes(5));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class GraphSequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public int Calls { get; private set; }
        public List<string?> AuthorizationSchemes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            AuthorizationSchemes.Add(request.Headers.Authorization?.Scheme);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No Graph response was configured.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responses.Dequeue(),
                    Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    private sealed class OversizedJsonHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const int safeLimit = 8 * 1024 * 1024;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(
                    new MemoryStream(
                        new byte[safeLimit + 1],
                        writable: false)),
            });
        }
    }

    private sealed class AuthorizationEvidenceHandler(
        string? denyResponse = null,
        HttpStatusCode denyStatusCode = HttpStatusCode.OK,
        string? roleAssignmentCondition = null,
        string? roleAssignmentNextLink = null,
        bool usesAzureRbac = true) : HttpMessageHandler
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ??
                throw new InvalidOperationException("Request URI is required.");
            Requests.Add(new AuthorizationRequest(
                request.Method,
                uri,
                request.Headers.Authorization?.Scheme));

            if (uri.AbsolutePath.Equals(VaultScope, StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse(
                    $$"""
                    {
                      "properties": {
                        "tenantId": "22222222-2222-2222-2222-222222222222",
                        "enableRbacAuthorization": {{JsonSerializer.Serialize(usesAzureRbac)}}
                      }
                    }
                    """);
            }

            if (uri.AbsolutePath.EndsWith(
                    "/providers/Microsoft.Authorization/permissions",
                    StringComparison.OrdinalIgnoreCase))
            {
                var isIdentity = uri.AbsolutePath.StartsWith(
                    IdentityScope,
                    StringComparison.OrdinalIgnoreCase);
                return JsonResponse(
                    isIdentity
                        ? """
                          {
                            "value": [
                              {
                                "actions": [
                                  "Microsoft.ManagedIdentity/userAssignedIdentities/*/read",
                                  "Microsoft.ManagedIdentity/userAssignedIdentities/*/assign/action"
                                ],
                                "notActions": [],
                                "dataActions": [],
                                "notDataActions": []
                              }
                            ]
                          }
                          """
                        : """
                          {
                            "value": [
                              {
                                "actions": [
                                  "Microsoft.Authorization/roleAssignments/write"
                                ],
                                "notActions": [],
                                "dataActions": [],
                                "notDataActions": []
                              }
                            ]
                          }
                          """);
            }

            if (uri.AbsolutePath.EndsWith(
                    "/providers/Microsoft.Authorization/roleAssignments",
                    StringComparison.OrdinalIgnoreCase))
            {
                var nextLink = roleAssignmentNextLink is null
                    ? string.Empty
                    : $""", "nextLink": "{roleAssignmentNextLink}" """;
                var condition = roleAssignmentCondition is null
                    ? "null"
                    : JsonSerializer.Serialize(roleAssignmentCondition);
                return JsonResponse(
                    $$"""
                    {
                      "value": [
                        {
                          "properties": {
                            "roleDefinitionId": "{{RoleDefinitionId}}",
                            "principalId": "{{PrincipalId}}",
                            "scope": "/subscriptions/{{SubscriptionId}}",
                            "condition": {{condition}}
                          }
                        }
                      ]{{nextLink}}
                    }
                    """);
            }

            if (uri.AbsolutePath.EndsWith(
                    "/providers/Microsoft.Authorization/denyAssignments",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (denyStatusCode != HttpStatusCode.OK)
                    return Task.FromResult(new HttpResponseMessage(denyStatusCode));
                return JsonResponse(denyResponse ?? """{ "value": [] }""");
            }

            if (uri.AbsolutePath.Equals(
                    RoleDefinitionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return JsonResponse(
                    """
                    {
                      "properties": {
                        "roleName": "Vault Prospector test role",
                        "permissions": [
                          {
                            "actions": [],
                            "notActions": [],
                            "dataActions": [
                              "Microsoft.KeyVault/vaults/secrets/readMetadata/action",
                              "Microsoft.KeyVault/vaults/*/read",
                              "Microsoft.KeyVault/vaults/secrets/getSecret/action"
                            ],
                            "notDataActions": []
                          }
                        ]
                      }
                    }
                    """);
            }

            throw new InvalidOperationException(
                $"No test response is configured for {uri}.");
        }

        private static Task<HttpResponseMessage> JsonResponse(string json) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"),
            });
    }

    private sealed record AuthorizationRequest(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme);

    private sealed class FixedEnterprisePolicy(
        EnterprisePolicySnapshot snapshot)
        : IEnterprisePolicy
    {
        public EnterprisePolicySnapshot GetSnapshot() => snapshot;
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool SawMetadataHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            SawMetadataHeader = request.Headers.TryGetValues("Metadata", out var values) &&
                values.Contains("true", StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
