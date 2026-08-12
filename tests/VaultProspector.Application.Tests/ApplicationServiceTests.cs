using VaultProspector.Application;
using VaultProspector.Domain;

namespace VaultProspector.Application.Tests;

public sealed class ApplicationServiceTests
{
    [Fact]
    public void SynchronizationProviderContractHasNoInteractiveDiscoveryPath()
    {
        Assert.DoesNotContain(
            typeof(IVaultProvider).GetMethods(),
            method => method.Name.Contains(
                "Interactive",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LocalDataRecoveryRejectsIncorrectConfirmationBeforeVerification()
    {
        var verification = new CountingVerify();
        var resetter = new FakeResetter();
        var service = new LocalDataRecoveryService(verification, resetter);

        await Assert.ThrowsAsync<LocalDataResetConfirmationException>(() =>
            service.ArchiveAndResetAsync("reset", TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, resetter.Calls);
    }

    [Fact]
    public async Task LocalDataRecoveryPreservesStateWhenVerificationIsNotCompleted()
    {
        var resetter = new FakeResetter();
        var service = new LocalDataRecoveryService(new NeverVerify(), resetter);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ArchiveAndResetAsync("RESET", TestContext.Current.CancellationToken));

        Assert.Equal(0, resetter.Calls);
    }

    [Fact]
    public async Task LocalDataRecoveryArchivesOnlyAfterConfirmationAndVerification()
    {
        var resetter = new FakeResetter
        {
            Result = new LocalDataArchive("recovery-path", true),
        };
        var service = new LocalDataRecoveryService(new AlwaysVerify(), resetter);

        var result = await service.ArchiveAndResetAsync(
            " RESET ",
            TestContext.Current.CancellationToken);

        Assert.True(result.HadExistingData);
        Assert.Equal("recovery-path", result.ArchivePath);
        Assert.Equal(1, resetter.Calls);
    }

    [Fact]
    public async Task IdentityAdditionRejectsInvalidClientIdBeforeAuthentication()
    {
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, new FakeRepository(Identity()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(
            "../../outside-cache",
            "Test",
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.SignInCalls);
    }

    [Fact]
    public async Task IdentityAdditionCanonicalizesClientIdBeforeAuthentication()
    {
        var provider = new FakeIdentityProvider();
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(provider, repository);

        await service.AddAsync(
            "{11111111-1111-1111-1111-111111111111}",
            " Test ",
            TestContext.Current.CancellationToken);

        Assert.Equal("11111111-1111-1111-1111-111111111111", provider.ClientId);
        Assert.Equal("Test", provider.DisplayName);
        Assert.NotNull(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task IdentityAdditionRollsBackTokenCacheWhenMetadataPersistenceFails()
    {
        var provider = new FakeIdentityProvider();
        var repository = new FakeRepository(Identity())
        {
            UpsertIdentityException = new InvalidOperationException("metadata unavailable"),
        };
        var service = new IdentityService(provider, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(
            "11111111-1111-1111-1111-111111111111",
            "Test",
            TestContext.Current.CancellationToken));

        Assert.Equal(1, provider.RemoveCalls);
        Assert.Same(repository.UpsertedIdentity, provider.RemovedIdentity);
    }

    [Fact]
    public async Task EnterprisePolicyDeniesAzureProviderBeforeInteractiveSignIn()
    {
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(
            provider,
            new FakeRepository(Identity()),
            new FakeDiagnostics(),
            enterprisePolicy: new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedProviders:
                        [EnterpriseProvider.CyberArkPrivilegeCloud])));

        var error = await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.AddAsync(
                "11111111-1111-1111-1111-111111111111",
                "Test",
                TestContext.Current.CancellationToken));

        Assert.Equal("AllowedProviders", error.PolicyName);
        Assert.Equal(0, provider.SignInCalls);
    }

    [Fact]
    public async Task EnterpriseTenantPolicyRollsBackInteractiveTokenCache()
    {
        var provider = new FakeIdentityProvider();
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(
            provider,
            repository,
            new FakeDiagnostics(),
            enterprisePolicy: new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds:
                        ["22222222-2222-2222-2222-222222222222"])));

        var error = await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.AddAsync(
                "11111111-1111-1111-1111-111111111111",
                "Test",
                TestContext.Current.CancellationToken));

        Assert.Equal("AllowedTenantIds", error.PolicyName);
        Assert.Equal(1, provider.SignInCalls);
        Assert.Equal(1, provider.RemoveCalls);
        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task EnterpriseTenantPolicyDeniesWorkloadBeforeCredentialValidation()
    {
        var provider = new FakeIdentityProvider();
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(
            provider,
            repository,
            new FakeDiagnostics(),
            enterprisePolicy: new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds:
                        ["33333333-3333-3333-3333-333333333333"])));

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.AddWorkloadIdentityAsync(
                "11111111-1111-1111-1111-111111111111",
                "22222222-2222-2222-2222-222222222222",
                "Automation",
                IdentityType.ServicePrincipal,
                "AA11BB22CC33DD44EE55FF660011223344556677",
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.ReauthenticateCalls);
        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task ServicePrincipalProfileCanonicalizesIdentifiersAndCertificateThumbprint()
    {
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(new FakeIdentityProvider(), repository);

        var identity = await service.AddWorkloadIdentityAsync(
            "{11111111-1111-1111-1111-111111111111}",
            "{22222222-2222-2222-2222-222222222222}",
            " Automation ",
            IdentityType.ServicePrincipal,
            "aa11 bb22 cc33 dd44 ee55 ff66 0011 2233 4455 6677",
            TestContext.Current.CancellationToken);

        Assert.Equal("11111111-1111-1111-1111-111111111111", identity.ClientId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", identity.HomeTenantId);
        Assert.Equal("AA11BB22CC33DD44EE55FF660011223344556677", identity.CredentialData);
        Assert.Equal("Automation", identity.DisplayName);
        Assert.Equal(IdentityType.ServicePrincipal, repository.UpsertedIdentity?.Type);
    }

    [Theory]
    [InlineData("", "22222222-2222-2222-2222-222222222222", "AA11BB22CC33DD44EE55FF660011223344556677")]
    [InlineData("11111111-1111-1111-1111-111111111111", "", "AA11BB22CC33DD44EE55FF660011223344556677")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "not-a-thumbprint")]
    public async Task ServicePrincipalProfileRejectsIncompleteOrInvalidConfiguration(
        string clientId,
        string tenantId,
        string thumbprint)
    {
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(new FakeIdentityProvider(), repository);

        await Assert.ThrowsAsync<WorkloadIdentityConfigurationException>(() => service.AddWorkloadIdentityAsync(
            clientId,
            tenantId,
            "Automation",
            IdentityType.ServicePrincipal,
            thumbprint,
            TestContext.Current.CancellationToken));

        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task ManagedIdentityProfileRejectsCredentialMaterial()
    {
        var repository = new FakeRepository(Identity());
        var service = new IdentityService(new FakeIdentityProvider(), repository);

        await Assert.ThrowsAsync<WorkloadIdentityConfigurationException>(() => service.AddWorkloadIdentityAsync(
            string.Empty,
            string.Empty,
            "Azure host",
            IdentityType.ManagedIdentity,
            "client-secret-must-never-be-stored",
            TestContext.Current.CancellationToken));

        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task WorkloadProfileIsNotPersistedWhenCredentialValidationFails()
    {
        var repository = new FakeRepository(Identity());
        var provider = new FakeIdentityProvider
        {
            ReauthenticateException = new InvalidOperationException("credential unavailable"),
        };
        var service = new IdentityService(provider, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddWorkloadIdentityAsync(
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            "Automation",
            IdentityType.ServicePrincipal,
            "AA11BB22CC33DD44EE55FF660011223344556677",
            TestContext.Current.CancellationToken));

        Assert.Equal(1, provider.ReauthenticateCalls);
        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task FederatedProfileStoresOnlyCanonicalReadableTokenFilePath()
    {
        var tokenPath = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-federated-{Guid.NewGuid():N}.token");
        await File.WriteAllTextAsync(
            tokenPath,
            "test-token-content",
            TestContext.Current.CancellationToken);
        try
        {
            var repository = new FakeRepository(Identity());
            var provider = new FakeIdentityProvider();
            var service = new IdentityService(provider, repository);

            var identity = await service.AddWorkloadIdentityAsync(
                "11111111-1111-1111-1111-111111111111",
                "22222222-2222-2222-2222-222222222222",
                "Federated automation",
                IdentityType.FederatedServicePrincipal,
                tokenPath,
                TestContext.Current.CancellationToken);

            Assert.Equal(Path.GetFullPath(tokenPath), identity.CredentialData);
            Assert.Equal(IdentityType.FederatedServicePrincipal, identity.Type);
            Assert.Equal(1, provider.ReauthenticateCalls);
            Assert.Equal(identity, repository.UpsertedIdentity);
            Assert.DoesNotContain("test-token-content", identity.CredentialData, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tokenPath);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-federated-token-file")]
    public async Task FederatedProfileRejectsMissingOrUnreadableTokenFile(string path)
    {
        var repository = new FakeRepository(Identity());
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, repository);

        await Assert.ThrowsAsync<WorkloadIdentityConfigurationException>(() =>
            service.AddWorkloadIdentityAsync(
                "11111111-1111-1111-1111-111111111111",
                "22222222-2222-2222-2222-222222222222",
                "Federated automation",
                IdentityType.FederatedServicePrincipal,
                path,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.ReauthenticateCalls);
        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task CertificateRotationValidatesReplacementBeforePersistence()
    {
        var original = Identity() with
        {
            Type = IdentityType.ServicePrincipal,
            CredentialData = new string('A', 40),
        };
        var repository = new FakeRepository(original);
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, repository);

        await service.RotateWorkloadCredentialAsync(
            original.Id,
            "bb22 cc33 dd44 ee55 ff66 0011 2233 4455 6677 8899",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.ReauthenticateCalls);
        Assert.Equal(
            "BB22CC33DD44EE55FF6600112233445566778899",
            provider.ReauthenticatedIdentity?.CredentialData);
        Assert.Equal(provider.ReauthenticatedIdentity?.CredentialData, repository.UpsertedIdentity?.CredentialData);
        Assert.Equal(AuthenticationState.Ready, repository.UpsertedIdentity?.AuthenticationState);
    }

    [Fact]
    public async Task FailedCredentialRotationPreservesPersistedProfile()
    {
        var original = Identity() with
        {
            Type = IdentityType.ServicePrincipal,
            CredentialData = new string('A', 40),
        };
        var repository = new FakeRepository(original);
        var provider = new FakeIdentityProvider
        {
            ReauthenticateException = new InvalidOperationException("replacement rejected"),
        };
        var service = new IdentityService(provider, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RotateWorkloadCredentialAsync(
                original.Id,
                "BB22CC33DD44EE55FF6600112233445566778899",
                TestContext.Current.CancellationToken));

        Assert.Null(repository.UpsertedIdentity);
    }

    [Fact]
    public async Task LocalRevocationFailsClosedAndRemovesCredentialReference()
    {
        var firstVaultId = Guid.NewGuid();
        var secondVaultId = Guid.NewGuid();
        var original = Identity() with
        {
            Type = IdentityType.ServicePrincipal,
            CredentialData = new string('A', 40),
        };
        var repository = new FakeRepository(original)
        {
            VaultIds = [firstVaultId, secondVaultId, firstVaultId],
        };
        var provider = new FakeIdentityProvider();
        var values = new FakeValueStore();
        var service = new IdentityService(
            provider,
            repository,
            new FakeDiagnostics(),
            values);

        var result = await service.RevokeLocalAccessAsync(
            original.Id,
            TestContext.Current.CancellationToken);

        Assert.False(repository.UpsertedIdentity?.IsEnabled);
        Assert.Equal(AuthenticationState.Revoked, repository.UpsertedIdentity?.AuthenticationState);
        Assert.Equal(string.Empty, repository.UpsertedIdentity?.CredentialData);
        Assert.Equal(1, provider.RemoveCalls);
        Assert.True(result.ProviderCredentialRemoved);
        Assert.Equal(2, result.PurgedVaultCount);
        Assert.Equal(
            [firstVaultId, secondVaultId],
            values.PurgedVaultIds);
        Assert.All(
            values.PurgeCancellationTokens,
            token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task ProviderCleanupFailureDoesNotSkipOfflinePurge()
    {
        var vaultId = Guid.NewGuid();
        var original = Identity() with
        {
            Type = IdentityType.ServicePrincipal,
            CredentialData = new string('A', 40),
        };
        var repository = new FakeRepository(original)
        {
            VaultIds = [vaultId],
        };
        var provider = new FakeIdentityProvider
        {
            RemoveException =
                new InvalidOperationException("provider cleanup failed"),
        };
        var values = new FakeValueStore();
        var diagnostics = new FakeDiagnostics();
        var service = new IdentityService(
            provider,
            repository,
            diagnostics,
            values);

        var result = await service.RevokeLocalAccessAsync(
            original.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.ProviderCredentialRemoved);
        Assert.Equal(1, result.PurgedVaultCount);
        Assert.Equal([vaultId], values.PurgedVaultIds);
        Assert.Contains(
            "identity_provider_credential_removal_failed",
            diagnostics.ErrorEvents);
    }

    [Fact]
    public async Task OfflinePurgeFailureContinuesAndReportsRevokedState()
    {
        var failedVaultId = Guid.NewGuid();
        var retainedAttemptId = Guid.NewGuid();
        var original = Identity() with
        {
            Type = IdentityType.ServicePrincipal,
            CredentialData = new string('A', 40),
        };
        var repository = new FakeRepository(original)
        {
            VaultIds = [failedVaultId, retainedAttemptId],
        };
        var values = new FakeValueStore
        {
            PurgeFailureVaultId = failedVaultId,
        };
        var diagnostics = new FakeDiagnostics();
        var service = new IdentityService(
            new FakeIdentityProvider(),
            repository,
            diagnostics,
            values);

        var exception =
            await Assert.ThrowsAsync<LocalRevocationCleanupException>(
                () => service.RevokeLocalAccessAsync(
                    original.Id,
                    TestContext.Current.CancellationToken));

        Assert.Equal(1, exception.FailedVaultCount);
        Assert.Equal(AuthenticationState.Revoked, repository.UpsertedIdentity?.AuthenticationState);
        Assert.Equal(
            [failedVaultId, retainedAttemptId],
            values.PurgedVaultIds);
        Assert.Contains(
            "identity_offline_value_purge_failed",
            diagnostics.ErrorEvents);
    }

    [Fact]
    public async Task IdentityScopedOfflinePurgeIncludesRemovedAccessAndDeduplicatesVaults()
    {
        var firstVaultId = Guid.NewGuid();
        var secondVaultId = Guid.NewGuid();
        var identity = Identity();
        var repository = new FakeRepository(identity)
        {
            VaultIds = [firstVaultId, secondVaultId, firstVaultId],
        };
        var values = new FakeValueStore();
        var service = new IdentityService(
            new FakeIdentityProvider(),
            repository,
            new FakeDiagnostics(),
            values);

        var purgedVaultCount = await service.PurgeOfflineValuesAsync(
            identity.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, purgedVaultCount);
        Assert.Equal([firstVaultId, secondVaultId], values.PurgedVaultIds);
    }

    [Fact]
    public async Task EnableRevalidatesCredentialBeforeChangingPersistedState()
    {
        var disabled = Identity() with
        {
            IsEnabled = false,
            AuthenticationState = AuthenticationState.Disabled,
        };
        var repository = new FakeRepository(disabled);
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, repository);

        await service.EnableAsync(disabled.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.ReauthenticateCalls);
        Assert.True(repository.UpsertedIdentity?.IsEnabled);
        Assert.Equal(AuthenticationState.Ready, repository.UpsertedIdentity?.AuthenticationState);
    }

    [Fact]
    public async Task DirectoryReadAuthorizationRequiresInteractiveIdentityAndPersistsResult()
    {
        var interactive = Identity();
        var repository = new FakeRepository(interactive);
        var provider = new FakeIdentityProvider();
        var service = new IdentityService(provider, repository);

        await service.AuthorizeDirectoryReadAsync(
            interactive.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, provider.DirectoryAuthorizeCalls);
        Assert.Equal(AuthenticationState.Ready, repository.UpsertedIdentity?.AuthenticationState);

        var workloadRepository = new FakeRepository(interactive with
        {
            Type = IdentityType.ServicePrincipal,
        });
        var workloadService = new IdentityService(provider, workloadRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workloadService.AuthorizeDirectoryReadAsync(
                interactive.Id,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SynchronizationPersistsPartialSuccessAndSafeErrors()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider { Snapshot = new DiscoverySnapshot([], [], [], [], [], [new ProviderError("scope", "Forbidden", "Azure request failed with status 403.")]) };
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());

        var run = await service.SynchronizeAsync(identity, TestContext.Current.CancellationToken);

        Assert.Equal(SyncStatus.CompletedWithErrors, run.Status);
        Assert.NotNull(repository.AppliedSnapshot);
        Assert.Single(run.NonSensitiveErrors);
        var error = Assert.Single(run.ErrorDetails!);
        Assert.Equal("scope", error.Scope);
        Assert.Equal("Forbidden", error.Category);
        Assert.Contains("safe category", error.Recovery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SynchronizationRetriesOnlySelectedFailedScopeAsPatch()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider
        {
            Snapshot = new DiscoverySnapshot(
                [],
                [],
                [],
                [],
                [],
                []),
        };
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics());
        var failed = new SyncErrorDetail(
            "vault:redacted:secrets",
            "RequestFailedException",
            "Azure request failed with status 429.",
            "Retry.",
            RetryScope: new ProviderRetryScope(
                VaultResourceId:
                    "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/exact"));

        var run = await service.RetryFailedScopesAsync(
            identity,
            [failed],
            TestContext.Current.CancellationToken);

        Assert.Equal(SyncStatus.Completed, run.Status);
        Assert.Equal(1, repository.ApplyPatchCalls);
        Assert.Equal(0, repository.ApplyFullCalls);
        Assert.Equal(
            [
                "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/exact",
            ],
            provider.Constraints?.AllowedVaultResourceIds);
        Assert.Empty(provider.ExcludedSubscriptions);
        Assert.Empty(provider.ExcludedVaultResourceIds);
    }

    [Fact]
    public async Task SynchronizationDoesNotPersistAuthenticationExceptionMessages()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider
        {
            DiscoveryException = new AuthenticationFailedException(
                "secret-token-and-tenant-details"),
        };
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics());

        var run = await service.SynchronizeAsync(
            identity,
            TestContext.Current.CancellationToken);

        Assert.Equal(SyncStatus.Failed, run.Status);
        Assert.Equal(
            "Interactive Microsoft Entra authentication is required.",
            Assert.Single(run.NonSensitiveErrors));
        Assert.DoesNotContain("secret-token", run.NonSensitiveErrors[0], StringComparison.Ordinal);
        Assert.Equal(
            AuthenticationState.InteractionRequired,
            repository.UpsertedIdentity?.AuthenticationState);
    }

    [Fact]
    public async Task SynchronizationRejectsRevokedIdentityBeforeProviderCall()
    {
        var identity = Identity() with
        {
            IsEnabled = false,
            AuthenticationState = AuthenticationState.Revoked,
        };
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider();
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SynchronizeAsync(identity, TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task SynchronizationUsesPersistedIdentityStateInsteadOfStaleCallerState()
    {
        var staleCallerIdentity = Identity();
        var persistedIdentity = staleCallerIdentity with
        {
            IsEnabled = false,
            AuthenticationState = AuthenticationState.Revoked,
        };
        var repository = new FakeRepository(persistedIdentity);
        var provider = new FakeProvider();
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SynchronizeAsync(staleCallerIdentity, TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task SynchronizationExcludesOnlySubscriptionsDisabledForTheSelectedIdentity()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity)
        {
            Subscriptions =
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "included", "Included", "Enabled", true, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), Guid.NewGuid(), "excluded", "Excluded", "Enabled", false, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), Guid.NewGuid(), "EXCLUDED", "Duplicate", "Enabled", false, DateTimeOffset.UtcNow),
            ],
        };
        var provider = new FakeProvider();
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());

        await service.SynchronizeAsync(identity, TestContext.Current.CancellationToken);

        Assert.Equal(identity.Id, repository.RequestedSubscriptionIdentityId);
        Assert.Equal(["excluded"], provider.ExcludedSubscriptions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SynchronizationExcludesPersistedTenantWithoutRemovingItsKnownVaults()
    {
        var identity = Identity();
        var excludedTenant = new TenantAccess(
            Guid.NewGuid(),
            identity.Id,
            "22222222-2222-2222-2222-222222222222",
            "Excluded tenant",
            "AAD",
            DateTimeOffset.UtcNow,
            "Available",
            false);
        var excludedVault = Vault() with
        {
            TenantId = excludedTenant.TenantId,
        };
        var repository = new FakeRepository(identity)
        {
            Tenants = [excludedTenant],
            VaultAccessSummaries =
            [
                new(
                    excludedVault,
                    new VaultAccess(
                        Guid.NewGuid(),
                        excludedVault.Id,
                        identity.Id,
                        excludedTenant.TenantId,
                        "Visible",
                        DateTimeOffset.UtcNow,
                        null,
                        0,
                        true),
                    identity.DisplayName,
                    excludedTenant.DisplayName),
            ],
        };
        var provider = new FakeProvider();
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics());

        await service.SynchronizeAsync(identity, TestContext.Current.CancellationToken);

        Assert.NotNull(provider.Constraints);
        Assert.Contains(excludedTenant.TenantId, provider.Constraints.ExcludedTenantIds);
        Assert.False(provider.Constraints.IsTenantAllowed(excludedTenant.TenantId));
        Assert.Contains(repository.AppliedSnapshot!.Vaults, vault => vault.Id == excludedVault.Id);
    }

    [Fact]
    public async Task SynchronizationExcludesOnlyVaultAccessPathsDisabledForTheSelectedIdentity()
    {
        var identity = Identity();
        var includedVault = Vault();
        var excludedVault = Vault() with
        {
            Id = Guid.NewGuid(),
            ProviderResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/excluded",
            Name = "excluded",
            VaultUri = new Uri("https://excluded.vault.azure.net/"),
        };
        var repository = new FakeRepository(identity)
        {
            VaultAccessSummaries =
            [
                new(
                    includedVault,
                    new VaultAccess(Guid.NewGuid(), includedVault.Id, identity.Id, "tenant", "Visible", DateTimeOffset.UtcNow, null, 0, true),
                    identity.DisplayName,
                    "Tenant"),
                new(
                    excludedVault,
                    new VaultAccess(Guid.NewGuid(), excludedVault.Id, identity.Id, "tenant", "Visible", DateTimeOffset.UtcNow, null, 0, false),
                    identity.DisplayName,
                    "Tenant"),
            ],
        };
        var provider = new FakeProvider();
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());

        await service.SynchronizeAsync(identity, TestContext.Current.CancellationToken);

        Assert.Equal([excludedVault.ProviderResourceId], provider.ExcludedVaultResourceIds, StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(repository.AppliedSnapshot);
        Assert.Contains(repository.AppliedSnapshot.Vaults, vault => vault.Id == excludedVault.Id);
        Assert.Contains(repository.AppliedSnapshot.AccessPaths, access => access.VaultId == excludedVault.Id && !access.IsSelected);
    }

    [Fact]
    public async Task SecretRetrievalRejectsKeysWithoutCallingProvider()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Key);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, provider.RetrieveCalls);
    }

    [Fact]
    public async Task CancellationAfterProviderReturnPreventsClipboardReleaseAndDisposesValue()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), clipboard, new AlwaysVerify(), new FixedClock());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            CachePolicy.SecureDefault,
            cancellation.Token));

        Assert.Equal(1, provider.RetrieveCalls);
        Assert.Equal(0, clipboard.CopyCalls);
        Assert.True(provider.LastRetrievedValue?.IsDisposed);
    }

    [Fact]
    public async Task SecretRetrievalRequiresLocalVerification()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, new FakeValueStore(), new FakeClipboard(), new NeverVerify(), new FixedClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevealGraceIsUsedOnlyByExplicitReveal()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity)
        {
            Resolved = (item, Vault(), identity),
        };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var verification = new CountingVerify();
        var revealSession = new TrackingRevealVerificationSession();
        var service = new SecretAccessService(
            provider,
            repository,
            new FakeValueStore(),
            clipboard,
            verification,
            new FixedClock(),
            revealVerificationSession: revealSession);

        {
            using var revealed = await service.RetrieveAsync(
                item.Id,
                TimeSpan.FromSeconds(60),
                TestContext.Current.CancellationToken);
            Assert.False(revealed.IsDisposed);
        }
        await service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            new CachePolicy(
                false,
                TimeSpan.FromHours(8),
                true,
                true),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, revealSession.CallCount);
        Assert.Equal(TimeSpan.FromSeconds(60), revealSession.RequestedGrace);
        Assert.Equal(1, verification.Calls);
        Assert.Equal(2, provider.RetrieveCalls);
        Assert.Equal(1, clipboard.CopyCalls);
    }

    [Fact]
    public async Task SecretRetrievalDisposesValueWhenAccessHistoryCannotBeRecorded()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity)
        {
            Resolved = (item, Vault(), identity),
            RecordAccessException = new InvalidOperationException("metadata unavailable"),
        };
        var provider = new FakeProvider();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.NotNull(provider.LastRetrievedValue);
        Assert.True(provider.LastRetrievedValue.IsDisposed);
    }

    [Fact]
    public async Task CopyRequiresVerificationBeforeAzureOrClipboardAccess()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), clipboard, new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            new CachePolicy(false, TimeSpan.FromHours(8), true, true),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, clipboard.CopyCalls);
    }

    [Fact]
    public async Task CopyUsesOneVerificationAndCopiesOneRetrievedValue()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var clipboard = new FakeClipboard();
        var verification = new CountingVerify();
        var service = new SecretAccessService(provider, repository, new FakeValueStore(), clipboard, verification, new FixedClock());

        await service.RetrieveAndCopyAsync(
            item.Id,
            TimeSpan.FromSeconds(30),
            new CachePolicy(false, TimeSpan.FromHours(8), true, true),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, provider.RetrieveCalls);
        Assert.Equal(1, clipboard.CopyCalls);
        Assert.Equal("value", clipboard.CopiedValue);
    }

    [Fact]
    public async Task DisabledOfflinePolicyRejectsBeforeVerificationOrAzureRetrieval()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var verification = new CountingVerify();
        var store = new FakeValueStore();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), verification, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveAndCacheAsync(
            item.Id,
            null,
            TimeSpan.FromHours(1),
            CachePolicy.SecureDefault,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task OfflineRetrievalRequiresVerificationAndDoesNotCallAzure()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore { Value = "offline-value" };
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        using var value = await service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken);

        Assert.Equal("offline-value", value.Reveal());
        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(1, store.RetrieveCalls);
        Assert.Equal(1, repository.RecordAccessCalls);
    }

    [Fact]
    public async Task OfflineRetrievalDisposesValueWhenAccessHistoryCannotBeRecorded()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity)
        {
            Resolved = (item, Vault(), identity),
            RecordAccessException = new InvalidOperationException("metadata unavailable"),
        };
        var store = new FakeValueStore { Value = "offline-value" };
        var service = new SecretAccessService(new FakeProvider(), repository, store, new FakeClipboard(), new AlwaysVerify(), new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.NotNull(store.LastRetrievedValue);
        Assert.True(store.LastRetrievedValue.IsDisposed);
    }

    [Fact]
    public async Task OfflineRetrievalFailsClosedWhenVerificationIsRejected()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var store = new FakeValueStore { Value = "offline-value" };
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, store, new FakeClipboard(), new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.Equal(0, store.RetrieveCalls);
    }

    [Fact]
    public async Task OfflineRetrievalRejectsNonSecretMetadataBeforeVerificationOrCacheAccess()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Key);
        var store = new FakeValueStore { Value = "offline-value" };
        var verification = new CountingVerify();
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var service = new SecretAccessService(new FakeProvider(), repository, store, new FakeClipboard(), verification, new FixedClock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrieveCachedAsync(item.Id, TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, store.RetrieveCalls);
    }

    [Fact]
    public async Task RetrieveAndCacheUsesOneVerificationAndDisposesTheProviderValue()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore();
        var verification = new CountingVerify();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), verification, new FixedClock());

        await service.RetrieveAndCacheAsync(item.Id, null, TimeSpan.FromHours(1), new CachePolicy(true, TimeSpan.FromHours(2), true, true), TestContext.Current.CancellationToken);

        Assert.Equal(1, verification.Calls);
        Assert.Equal(1, provider.RetrieveCalls);
        Assert.Equal(1, store.StoreCalls);
        Assert.Equal("value", store.StoredValue);
        Assert.Equal("fingerprint", store.StoredFingerprint);
    }

    [Fact]
    public async Task OfflineCachingCannotDisableLocalVerificationThroughPolicy()
    {
        var identity = Identity();
        var item = Item(VaultObjectType.Secret);
        var repository = new FakeRepository(identity) { Resolved = (item, Vault(), identity) };
        var provider = new FakeProvider();
        var store = new FakeValueStore();
        var service = new SecretAccessService(provider, repository, store, new FakeClipboard(), new NeverVerify(), new FixedClock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetrieveAndCacheAsync(
            item.Id,
            null,
            TimeSpan.FromHours(1),
            new CachePolicy(true, TimeSpan.FromHours(2), false, true),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task SynchronizationReportsUserCancellationWithoutPersistingPartialState()
    {
        var identity = Identity();
        var repository = new FakeRepository(identity);
        var provider = new FakeProvider { HonorCancellation = true };
        var service = new SynchronizationService(provider, repository, new FixedClock(), new FakeDiagnostics());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var run = await service.SynchronizeAsync(identity, cancellation.Token);

        Assert.Equal(SyncStatus.Cancelled, run.Status);
        Assert.Null(repository.AppliedSnapshot);
    }

    [Fact]
    public async Task WorkspaceLinksUseExplicitResourceTypeAndIdentifier()
    {
        var repository = new FakeRepository(Identity());
        var service = new WorkspaceService(repository);
        var workspaceId = Guid.NewGuid();
        var vaultId = Guid.NewGuid().ToString("D");

        await service.AddResourceAsync(workspaceId, ResourceLinkType.Vault, vaultId, TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedLink);
        Assert.Equal(workspaceId, repository.AddedLink.WorkspaceId);
        Assert.Equal(ResourceLinkType.Vault, repository.AddedLink.ResourceType);
        Assert.Equal(vaultId, repository.AddedLink.ResourceId);
    }

    [Fact]
    public async Task EnterprisePolicyDeniesSynchronizationBeforeProviderRequest()
    {
        var identity = Identity() with
        {
            HomeTenantId =
                "11111111-1111-1111-1111-111111111111",
        };
        var provider = new FakeProvider();
        var repository = new FakeRepository(identity);
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics(),
            new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds:
                        ["22222222-2222-2222-2222-222222222222"])));

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.SynchronizeAsync(
                identity,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, provider.DiscoveryCalls);
    }

    [Fact]
    public async Task EnterpriseTenantPolicyConstrainsProviderAndPersistedSnapshot()
    {
        const string allowedTenantId =
            "11111111-1111-1111-1111-111111111111";
        const string deniedTenantId =
            "22222222-2222-2222-2222-222222222222";
        var identity = Identity() with { HomeTenantId = allowedTenantId };
        var allowedTenant = new TenantAccess(
            Guid.NewGuid(),
            identity.Id,
            allowedTenantId,
            "Allowed",
            "Home",
            DateTimeOffset.UtcNow,
            "Available");
        var deniedTenant = allowedTenant with
        {
            Id = Guid.NewGuid(),
            TenantId = deniedTenantId,
            DisplayName = "Denied",
        };
        var allowedVault = Vault() with
        {
            Id = Guid.NewGuid(),
            TenantId = allowedTenantId,
        };
        var deniedVault = Vault() with
        {
            Id = Guid.NewGuid(),
            TenantId = deniedTenantId,
        };
        var provider = new FakeProvider
        {
            Snapshot = new DiscoverySnapshot(
                [allowedTenant, deniedTenant],
                [
                    new(
                        Guid.NewGuid(),
                        allowedTenant.Id,
                        "allowed-subscription",
                        "Allowed",
                        "Enabled",
                        true,
                        DateTimeOffset.UtcNow),
                    new(
                        Guid.NewGuid(),
                        deniedTenant.Id,
                        "denied-subscription",
                        "Denied",
                        "Enabled",
                        true,
                        DateTimeOffset.UtcNow),
                ],
                [allowedVault, deniedVault],
                [
                    new(
                        Guid.NewGuid(),
                        allowedVault.Id,
                        identity.Id,
                        allowedTenantId,
                        "Allowed",
                        DateTimeOffset.UtcNow,
                        null,
                        0),
                    new(
                        Guid.NewGuid(),
                        deniedVault.Id,
                        identity.Id,
                        deniedTenantId,
                        "Allowed",
                        DateTimeOffset.UtcNow,
                        null,
                        0),
                ],
                [
                    Item(VaultObjectType.Secret) with
                    {
                        Id = Guid.NewGuid(),
                        VaultId = allowedVault.Id,
                    },
                    Item(VaultObjectType.Secret) with
                    {
                        Id = Guid.NewGuid(),
                        VaultId = deniedVault.Id,
                    },
                ],
                []),
        };
        var repository = new FakeRepository(identity);
        var service = new SynchronizationService(
            provider,
            repository,
            new FixedClock(),
            new FakeDiagnostics(),
            new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds: [allowedTenantId])));

        var run = await service.SynchronizeAsync(
            identity,
            TestContext.Current.CancellationToken);

        Assert.Equal(SyncStatus.Completed, run.Status);
        Assert.Equal(
            [allowedTenantId],
            provider.Constraints?.AllowedTenantIds);
        var applied = Assert.IsType<DiscoverySnapshot>(
            repository.AppliedSnapshot);
        Assert.Equal(allowedTenantId, Assert.Single(applied.Tenants).TenantId);
        Assert.Equal(
            "allowed-subscription",
            Assert.Single(applied.Subscriptions).SubscriptionId);
        Assert.Equal(allowedVault.Id, Assert.Single(applied.Vaults).Id);
        Assert.Equal(
            allowedVault.Id,
            Assert.Single(applied.AccessPaths).VaultId);
        Assert.Equal(allowedVault.Id, Assert.Single(applied.Items).VaultId);
    }

    [Fact]
    public async Task EnterpriseClipboardPolicyDeniesBeforeVerificationOrRetrieval()
    {
        var identity = Identity();
        var provider = new FakeProvider();
        var repository = new FakeRepository(identity)
        {
            Resolved = (Item(VaultObjectType.Secret), Vault(), identity),
        };
        var verification = new CountingVerify();
        var clipboard = new FakeClipboard();
        var service = new SecretAccessService(
            provider,
            repository,
            new FakeValueStore(),
            clipboard,
            verification,
            new FixedClock(),
            new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowClipboard: false)));

        await Assert.ThrowsAsync<EnterprisePolicyDeniedException>(
            () => service.RetrieveAndCopyAsync(
                repository.Resolved.Value.Item.Id,
                TimeSpan.FromSeconds(30),
                new CachePolicy(
                    false,
                    TimeSpan.FromHours(8),
                    true,
                    true),
                TestContext.Current.CancellationToken));

        Assert.Equal(0, verification.Calls);
        Assert.Equal(0, provider.RetrieveCalls);
        Assert.Equal(0, clipboard.CopyCalls);
    }

    [Fact]
    public async Task EnterpriseOfflineCachePolicyCapsRequestedLifetime()
    {
        var identity = Identity();
        var provider = new FakeProvider();
        var repository = new FakeRepository(identity)
        {
            Resolved = (Item(VaultObjectType.Secret), Vault(), identity),
        };
        var service = new SecretAccessService(
            provider,
            repository,
            new FakeValueStore(),
            new FakeClipboard(),
            new AlwaysVerify(),
            new FixedClock(),
            new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    maximumOfflineCacheLifetime:
                        TimeSpan.FromMinutes(30))));

        var result = await service.RetrieveAndCacheAsync(
            repository.Resolved.Value.Item.Id,
            null,
            TimeSpan.FromHours(4),
            new CachePolicy(
                true,
                TimeSpan.FromHours(8),
                true,
                true),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new FixedClock().UtcNow.AddMinutes(30),
            result.ExpiresAt);
    }

    [Fact]
    public async Task EnterpriseTenantPolicyFiltersEncryptedLocalSearch()
    {
        var identity = Identity();
        var allowedVault = Vault() with
        {
            TenantId =
                "11111111-1111-1111-1111-111111111111",
        };
        var deniedVault = Vault() with
        {
            TenantId =
                "22222222-2222-2222-2222-222222222222",
        };
        var repository = new FakeRepository(identity)
        {
            SearchResults =
            [
                new(
                    Item(VaultObjectType.Secret),
                    allowedVault,
                    "Allowed",
                    "Allowed",
                    false,
                    null,
                    false),
                new(
                    Item(VaultObjectType.Secret),
                    deniedVault,
                    "Denied",
                    "Denied",
                    false,
                    null,
                    false),
            ],
        };
        var service = new SearchService(
            repository,
            new FixedClock(),
            new FixedEnterprisePolicy(
                new EnterprisePolicySnapshot(
                    true,
                    allowedTenantIds:
                        ["11111111-1111-1111-1111-111111111111"])));

        var results = await service.SearchAsync(
            new SearchRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(allowedVault.Id, Assert.Single(results).Vault.Id);
    }

    private static ConnectedIdentity Identity() => new(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111", "account", "user@example.invalid", "Test", "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow);
    private static VaultResource Vault() => new(Guid.NewGuid(), "/subscriptions/redacted/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/test", "test", "tenant", "subscription", "rg", "eastus", new Dictionary<string, string>(), new Uri("https://test.vault.azure.net/"), DateTimeOffset.UtcNow);
    private static VaultItem Item(VaultObjectType type) { var vault = Vault(); return new(Guid.NewGuid(), vault.Id, "item", type, true, new Dictionary<string, string>(), null, null, null, null, "version", "fingerprint", DateTimeOffset.UtcNow); }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-16T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture); }
    private sealed class FakeProvider : IVaultProvider
    {
        public DiscoverySnapshot Snapshot { get; set; } = new([], [], [], [], [], []);
        public Exception? DiscoveryException { get; init; }
        public bool HonorCancellation { get; init; }
        public int DiscoveryCalls { get; private set; }
        public int RetrieveCalls { get; private set; }
        public SensitiveValue? LastRetrievedValue { get; private set; }
        public IReadOnlyList<string> ExcludedSubscriptions { get; private set; } = [];
        public IReadOnlyList<string> ExcludedVaultResourceIds { get; private set; } = [];
        public VaultDiscoveryConstraints? Constraints { get; private set; }
        public Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            CancellationToken cancellationToken)
        {
            if (HonorCancellation) cancellationToken.ThrowIfCancellationRequested();
            if (DiscoveryException is not null)
                return Task.FromException<DiscoverySnapshot>(DiscoveryException);
            DiscoveryCalls++;
            ExcludedSubscriptions = excludedSubscriptions.ToArray();
            ExcludedVaultResourceIds = excludedVaultResourceIds.ToArray();
            return Task.FromResult(Snapshot);
        }
        public Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            VaultDiscoveryConstraints constraints,
            CancellationToken cancellationToken)
        {
            Constraints = constraints;
            return DiscoverAsync(
                identity,
                excludedSubscriptions,
                excludedVaultResourceIds,
                cancellationToken);
        }
        public Task<SensitiveValue> RetrieveSecretAsync(ConnectedIdentity identity, VaultResource vault, VaultItem item, CancellationToken cancellationToken)
        {
            RetrieveCalls++;
            LastRetrievedValue = new SensitiveValue("value");
            return Task.FromResult(LastRetrievedValue);
        }
    }

    private sealed class AuthenticationFailedException(string message) : Exception(message);
    private sealed class FakeRepository(ConnectedIdentity identity) : IMetadataRepository
    {
        public DiscoverySnapshot? AppliedSnapshot { get; private set; }
        public int ApplyFullCalls { get; private set; }
        public int ApplyPatchCalls { get; private set; }
        public WorkspaceResourceLink? AddedLink { get; private set; }
        public ConnectedIdentity? UpsertedIdentity { get; private set; }
        public (VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)? Resolved { get; set; }
        public Exception? RecordAccessException { get; init; }
        public Exception? UpsertIdentityException { get; init; }
        public int RecordAccessCalls { get; private set; }
        public IReadOnlyList<SubscriptionAccess> Subscriptions { get; init; } = [];
        public IReadOnlyList<TenantAccess> Tenants { get; init; } = [];
        public IReadOnlyList<VaultAccessSummary> VaultAccessSummaries { get; init; } = [];
        public IReadOnlyList<Guid> VaultIds { get; init; } = [];
        public IReadOnlyList<SearchResult> SearchResults { get; init; } = [];
        public Guid? RequestedSubscriptionIdentityId { get; private set; }
        public Task InitializeAsync(CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<ConnectedIdentity>>([identity]);
        public Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken c) => Task.FromResult<ConnectedIdentity?>(identity);
        public Task UpsertIdentityAsync(ConnectedIdentity x, CancellationToken c)
        {
            UpsertedIdentity = x;
            return UpsertIdentityException is null ? Task.CompletedTask : Task.FromException(UpsertIdentityException);
        }
        public Task RemoveIdentityAsync(Guid id, CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(Guid identityId, CancellationToken c) =>
            Task.FromResult(Tenants);
        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(Guid identityId, CancellationToken c)
        {
            RequestedSubscriptionIdentityId = identityId;
            return Task.FromResult(Subscriptions);
        }
        public Task SetSubscriptionSelectedAsync(Guid subscriptionAccessId, bool isSelected, CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(Guid identityId, CancellationToken c) =>
            Task.FromResult(VaultAccessSummaries);
        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(Guid identityId, CancellationToken c) =>
            Task.FromResult(VaultIds);
        public Task SetVaultSelectedAsync(Guid vaultAccessId, bool isSelected, CancellationToken c) => Task.CompletedTask;
        public Task ApplyDiscoveryAsync(Guid id, DiscoverySnapshot snapshot, SyncRun run, CancellationToken c) { ApplyFullCalls++; AppliedSnapshot = snapshot; return Task.CompletedTask; }
        public Task ApplyDiscoveryPatchAsync(Guid id, DiscoverySnapshot snapshot, SyncRun run, CancellationToken c) { ApplyPatchCalls++; AppliedSnapshot = snapshot; return Task.CompletedTask; }
        public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest r, DateTimeOffset n, CancellationToken c) => Task.FromResult(SearchResults);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid id, CancellationToken c) => Task.FromResult(Resolved);
        public Task RecordAccessAsync(Guid id, DateTimeOffset at, CancellationToken c)
        {
            RecordAccessCalls++;
            return RecordAccessException is null ? Task.CompletedTask : Task.FromException(RecordAccessException);
        }
        public Task SetFavoriteAsync(Guid id, bool favorite, CancellationToken c) => Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken c) => Task.FromResult<IReadOnlyList<Workspace>>([]);
        public Task UpsertWorkspaceAsync(Workspace w, CancellationToken c) => Task.CompletedTask;
        public Task RemoveWorkspaceAsync(Guid id, CancellationToken c) => Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken c) { AddedLink = link; return Task.CompletedTask; }
        public Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken c) => Task.CompletedTask;
    }
    private sealed class FakeIdentityProvider : IIdentityProvider
    {
        public int SignInCalls { get; private set; }
        public int ReauthenticateCalls { get; private set; }
        public int DirectoryAuthorizeCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public string? ClientId { get; private set; }
        public string? DisplayName { get; private set; }
        public ConnectedIdentity? RemovedIdentity { get; private set; }
        public ConnectedIdentity? ReauthenticatedIdentity { get; private set; }
        public Exception? ReauthenticateException { get; init; }
        public Exception? RemoveException { get; init; }
        public Task<ConnectedIdentity> SignInAsync(string clientId, string displayName, CancellationToken cancellationToken)
        {
            SignInCalls++;
            ClientId = clientId;
            DisplayName = displayName;
            return Task.FromResult(new ConnectedIdentity(Guid.NewGuid(), clientId, "account", "user@example.invalid", displayName, "tenant", AuthenticationState.Ready, DateTimeOffset.UtcNow));
        }
        public Task<ConnectedIdentity> ReauthenticateAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
        {
            ReauthenticateCalls++;
            ReauthenticatedIdentity = identity;
            if (ReauthenticateException is not null)
                return Task.FromException<ConnectedIdentity>(ReauthenticateException);
            return Task.FromResult(identity with { AuthenticationState = AuthenticationState.Ready, LastInteractiveAuthentication = DateTimeOffset.UtcNow });
        }
        public Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
        {
            DirectoryAuthorizeCalls++;
            return Task.FromResult(identity with
            {
                AuthenticationState = AuthenticationState.Ready,
                LastInteractiveAuthentication = DateTimeOffset.UtcNow,
            });
        }
        public Task RemoveAsync(ConnectedIdentity identity, CancellationToken cancellationToken)
        {
            RemoveCalls++;
            RemovedIdentity = identity;
            return RemoveException is null
                ? Task.CompletedTask
                : Task.FromException(RemoveException);
        }
    }
    private sealed class FakeDiagnostics : IDiagnosticSink
    {
        public List<string> ErrorEvents { get; } = [];

        public void Information(
            string eventName,
            IReadOnlyDictionary<string, object?> fields)
        {
        }

        public void WriteError(
            string eventName,
            Exception exception,
            IReadOnlyDictionary<string, object?> fields) =>
            ErrorEvents.Add(eventName);
    }
    private sealed class FixedEnterprisePolicy(
        EnterprisePolicySnapshot snapshot)
        : IEnterprisePolicy
    {
        public EnterprisePolicySnapshot GetSnapshot() => snapshot;
    }
    private sealed class FakeResetter : ILocalDataResetter
    {
        public int Calls { get; private set; }
        public LocalDataArchive Result { get; init; } = new(string.Empty, false);

        public Task<LocalDataArchive> ArchiveForResetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
    private sealed class AlwaysVerify : IUserVerificationService { public bool IsAvailable => true; public Task<UserVerificationResult> VerifyAsync(string r, CancellationToken c) => Task.FromResult(UserVerificationResult.Verified); }
    private sealed class NeverVerify : IUserVerificationService { public bool IsAvailable => true; public Task<UserVerificationResult> VerifyAsync(string r, CancellationToken c) => Task.FromResult(UserVerificationResult.Canceled); }
    private sealed class CountingVerify : IUserVerificationService
    {
        public bool IsAvailable => true;
        public int Calls { get; private set; }
        public Task<UserVerificationResult> VerifyAsync(string r, CancellationToken c) { Calls++; return Task.FromResult(UserVerificationResult.Verified); }
    }
    private sealed class TrackingRevealVerificationSession :
        IRevealVerificationSession
    {
        public int CallCount { get; private set; }
        public TimeSpan RequestedGrace { get; private set; }

        public Task<bool> EnsureVerifiedAsync(
            TimeSpan requestedGracePeriod,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            RequestedGrace = requestedGracePeriod;
            return Task.FromResult(true);
        }

        public void Invalidate()
        {
        }
    }
    private sealed class FakeClipboard : IClipboardService
    {
        public int CopyCalls { get; private set; }
        public string? CopiedValue { get; private set; }
        public Task CopyWithAutoClearAsync(SensitiveValue v, TimeSpan d, CancellationToken c)
        {
            CopyCalls++;
            CopiedValue = v.Reveal();
            return Task.CompletedTask;
        }
    }
    private sealed class FakeValueStore : IProtectedValueStore
    {
        public string? Value { get; init; }
        public int RetrieveCalls { get; private set; }
        public int StoreCalls { get; private set; }
        public Guid? PurgeFailureVaultId { get; init; }
        public List<Guid> PurgedVaultIds { get; } = [];
        public List<CancellationToken> PurgeCancellationTokens { get; } = [];
        public SensitiveValue? LastRetrievedValue { get; private set; }
        public string? StoredValue { get; private set; }
        public string? StoredFingerprint { get; private set; }
        public Task<CachedSecretDescriptor> StoreAsync(Guid i, Guid v, Guid? w, SensitiveValue s, string f, DateTimeOffset e, CancellationToken c)
        {
            StoreCalls++;
            StoredValue = s.Reveal();
            StoredFingerprint = f;
            return Task.FromResult(new CachedSecretDescriptor(Guid.NewGuid(), i, v, w, DateTimeOffset.MinValue, e, null, f));
        }
        public Task<SensitiveValue?> RetrieveAsync(Guid i, DateTimeOffset n, string? f, CancellationToken c)
        {
            RetrieveCalls++;
            LastRetrievedValue = Value is null ? null : new SensitiveValue(Value);
            return Task.FromResult(LastRetrievedValue);
        }
        public Task PurgeItemAsync(Guid i, CancellationToken c) => Task.CompletedTask;
        public Task PurgeVaultAsync(Guid i, CancellationToken c)
        {
            PurgedVaultIds.Add(i);
            PurgeCancellationTokens.Add(c);
            return i == PurgeFailureVaultId
                ? Task.FromException(
                    new IOException("offline purge failed"))
                : Task.CompletedTask;
        }
        public Task PurgeWorkspaceAsync(Guid i, CancellationToken c) => Task.CompletedTask;
        public Task PurgeAllAsync(CancellationToken c) => Task.CompletedTask;
    }
}
