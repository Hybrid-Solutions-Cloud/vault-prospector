using System.Collections.Specialized;
using System.Text.Json;
using Avalonia.Controls;
using VaultProspector.App;
using VaultProspector.App.ViewModels;
using VaultProspector.Application;
using VaultProspector.Domain;
using VaultProspector.Providers.CyberArk;

namespace VaultProspector.App.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-app-tests-{Guid.NewGuid():N}");

    [Fact]
    public void CyberArkRoadmapFeatureIsNotVisibleInTheWindowsRelease()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsCyberArkPreviewEnabled);
    }

    [Fact]
    public async Task MissingSettingsUseProductRegistrationWithoutCustomMode()
    {
        var store = new AppSettingsStore(Path.Combine(_directory, "settings.json"));

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProductIdentity.DefaultClientId, settings.ClientId);
        Assert.False(settings.UseCustomClientId);
        Assert.Equal(0, settings.RevealVerificationGraceSeconds);
    }

    [Fact]
    public async Task LegacyEmptySettingsMigrateToProductRegistration()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { ClientId = "", ClipboardClearSeconds = 30, OfflineCacheEnabled = false, MaximumCacheHours = 8 }), TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProductIdentity.DefaultClientId, settings.ClientId);
        Assert.False(settings.UseCustomClientId);
    }

    [Fact]
    public async Task LegacyCustomClientIdIsPreservedAndInferred()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        const string customClientId = "11111111-1111-1111-1111-111111111111";
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { ClientId = customClientId, ClipboardClearSeconds = 30, OfflineCacheEnabled = false, MaximumCacheHours = 8 }), TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(customClientId, settings.ClientId);
        Assert.True(settings.UseCustomClientId);
    }

    [Fact]
    public async Task OversizedSettingsFileIsRejectedBeforeParsing()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await using (var stream = File.Create(path))
            stream.SetLength((64 * 1024) + 1);
        var store = new AppSettingsStore(path);

        await Assert.ThrowsAsync<JsonException>(
            () => store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NotificationAreaAndBackgroundPreferencesRoundTrip()
    {
        var store = new AppSettingsStore(Path.Combine(_directory, "settings.json"));
        var expected = AppSettings.Default with
        {
            CloseBehavior = CloseBehavior.LockToNotificationArea,
            BackgroundMetadataSyncEnabled = true,
            MinimizeToNotificationArea = false,
            RevealVerificationGraceSeconds = 60,
        };

        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var restored = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CloseBehavior.LockToNotificationArea, restored.CloseBehavior);
        Assert.True(restored.BackgroundMetadataSyncEnabled);
        Assert.False(restored.MinimizeToNotificationArea);
        Assert.Equal(60, restored.RevealVerificationGraceSeconds);
    }

    [Theory]
    [InlineData(true, WindowState.Minimized, true)]
    [InlineData(false, WindowState.Minimized, false)]
    [InlineData(true, WindowState.Normal, false)]
    [InlineData(true, WindowState.Maximized, false)]
    public void MinimizePolicyHidesOnlyConfiguredMinimizedWindows(
        bool enabled,
        WindowState state,
        bool expected) =>
        Assert.Equal(
            expected,
            WindowLifecyclePolicy.ShouldHideOnMinimize(enabled, state));

    [Theory]
    [InlineData(false, false, false, false, false, "Locked — offline")]
    [InlineData(true, true, false, false, false, "Locked — syncing metadata")]
    [InlineData(true, false, true, false, false, "Locked — action required")]
    [InlineData(true, false, false, false, false, "Locked")]
    [InlineData(false, false, false, true, false, "Offline")]
    [InlineData(true, true, false, true, false, "Syncing metadata")]
    [InlineData(true, false, true, true, false, "Action required")]
    [InlineData(true, false, false, true, true, "Azure interaction required")]
    [InlineData(true, false, false, true, false, "Ready")]
    public void TrayStatusPreservesLockAndOperationalContext(
        bool networkAvailable,
        bool isBusy,
        bool hasActionableError,
        bool isUnlocked,
        bool azureInteractionRequired,
        string expected)
    {
        Assert.Equal(
            expected,
            TrayStatusPolicy.Describe(
                networkAvailable,
                isBusy,
                hasActionableError,
                isUnlocked,
                azureInteractionRequired));
    }

    [Fact]
    public async Task ExplicitCustomModeWithNullClientIdFailsLaterAsActionableValidation()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{\"ClientId\":null,\"UseCustomClientId\":true}", TestContext.Current.CancellationToken);
        var store = new AppSettingsStore(path);

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, settings.ClientId);
        Assert.True(settings.UseCustomClientId);
    }

    [Fact]
    public void ErrorMappingProvidesRecoveryWithoutEchoingSensitiveMessage()
    {
        const string sensitiveMessage = "secret-value-that-must-not-appear";

        var error = UserFacingErrorMapper.From(new InvalidOperationException(sensitiveMessage));

        Assert.Equal("The action is blocked by current policy", error.Title);
        Assert.DoesNotContain(sensitiveMessage, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, error.Recovery, StringComparison.Ordinal);
        Assert.NotEmpty(error.Recovery);
    }

    [Fact]
    public void CyberArkFailureMappingDoesNotEchoProviderBody()
    {
        const string sensitiveMessage =
            "synthetic-client-credential-and-provider-body";
        var error = UserFacingErrorMapper.From(
            new CyberArkProviderException(
                "permission_denied",
                sensitiveMessage,
                System.Net.HttpStatusCode.Forbidden));

        var combined =
            $"{error.Title}|{error.Message}|{error.Recovery}";
        Assert.Contains(
            "CyberArk",
            combined,
            StringComparison.Ordinal);
        Assert.Contains(
            "least-privilege",
            combined,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            sensitiveMessage,
            combined,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActionableFailureBuildsOneCompleteScreenReaderAnnouncement()
    {
        Directory.CreateDirectory(_directory);
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new AppSettingsStore(Path.Combine(_directory, "settings.json")),
            new UnavailableVerificationService(),
            null!,
            null!);
        viewModel.UseCustomClientId = true;
        viewModel.ClientId = string.Empty;
        await viewModel.AddIdentityCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasActionableError);
        Assert.Equal("Starting securely…", viewModel.StatusText);
        Assert.Contains(viewModel.ErrorTitle, viewModel.ErrorAnnouncement, StringComparison.Ordinal);
        Assert.Contains(viewModel.ErrorMessage, viewModel.ErrorAnnouncement, StringComparison.Ordinal);
        Assert.Contains(viewModel.RecoveryText, viewModel.ErrorAnnouncement, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
        Assert.Empty(viewModel.ActiveOperationText);

        viewModel.UseCustomClientId = false;
        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasActionableError);
        Assert.Equal("Settings saved locally. No client secret is stored.", viewModel.StatusText);
    }

    [Fact]
    public async Task ConnectingTwoReadyIdentitiesClearsBusyStateAndEnablesSynchronization()
    {
        var repository = new EmptyRepository();
        var identityService = new IdentityService(
            new SequentialIdentityProvider(),
            repository);
        var viewModel = new MainViewModel(
            repository,
            identityService,
            null!,
            null!,
            null!,
            null!,
            null!,
            new AppSettingsStore(Path.Combine(_directory, "settings.json")),
            new UnavailableVerificationService(),
            null!,
            null!);

        viewModel.IdentityLabel = "First operator";
        await viewModel.AddIdentityCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsBusy);
        Assert.Empty(viewModel.ActiveOperationText);
        Assert.True(viewModel.SynchronizeCommand.CanExecute(null));

        viewModel.IdentityLabel = "Second operator";
        await viewModel.AddIdentityCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Identities.Count);
        Assert.Equal("Second operator", viewModel.SelectedIdentity?.DisplayName);
        Assert.False(viewModel.IsBusy);
        Assert.Empty(viewModel.ActiveOperationText);
        Assert.True(viewModel.SynchronizeCommand.CanExecute(null));
    }

    [Fact]
    public void WindowsVerificationFailureExplainsThatNoValueWasReleased()
    {
        var error = UserFacingErrorMapper.From(new UnauthorizedAccessException("internal"));

        Assert.Contains("not reveal", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Hello", error.Recovery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterprisePolicyFailureDoesNotMasqueradeAsWindowsVerification()
    {
        var error = UserFacingErrorMapper.From(
            new EnterprisePolicyDeniedException(
                "AllowedTenantIds",
                "internal policy detail"));

        Assert.Contains(
            "machine-managed",
            error.Title,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "HKLM",
            error.Recovery,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal policy detail",
            $"{error.Title}|{error.Message}|{error.Recovery}",
            StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptedSettingsFailureProvidesNarrowRecoveryWithoutDeletingProtectedData()
    {
        var error = UserFacingErrorMapper.From(new JsonException("internal file content"));

        Assert.Contains("settings.json", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not deleted", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal file content", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("internal file content", error.Recovery, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingProtectedKeyRecoveryDoesNotSuggestCreatingAReplacement()
    {
        var error = UserFacingErrorMapper.From(new ProtectedKeyUnavailableException("sensitive path"));

        Assert.Contains("key is unavailable", error.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without replacing", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matching", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archive-and-reset", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remove the local", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive path", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive path", error.Recovery, StringComparison.Ordinal);
    }

    [Fact]
    public void FutureSchemaRecoveryRequiresCompatibleApplicationVersion()
    {
        var error = UserFacingErrorMapper.From(new IncompatibleLocalDataVersionException(99, 2));

        Assert.Contains("newer", error.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("refused to modify", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same or a newer", error.Recovery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MetadataIntegrityRecoveryPreservesEvidenceOrAllowsExplicitReset()
    {
        var error = UserFacingErrorMapper.From(new LocalDataIntegrityException("sensitive database detail"));

        Assert.Contains("failed validation", error.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preserved", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("support", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archive-and-reset", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remove the local", error.Recovery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive database detail", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive database detail", error.Recovery, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkloadAuthorizationFailureExplainsReadEvidenceWithoutLeakingOperation()
    {
        var error = UserFacingErrorMapper.From(
            new WorkloadAuthorizationEvidenceException(
                403,
                "sensitive-resource-name"));

        var combined = $"{error.Title}|{error.Message}|{error.Recovery}";
        Assert.Contains(
            "authorization evidence",
            combined,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "denyAssignments/read",
            combined,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sensitive-resource-name",
            combined,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RevocationCleanupFailureExplainsThatLocalAccessIsAlreadyRevoked()
    {
        var error = UserFacingErrorMapper.From(
            new LocalRevocationCleanupException(
                1,
                new IOException("sensitive path")));

        var combined =
            $"{error.Title}|{error.Message}|{error.Recovery}";
        Assert.Contains(
            "access was revoked",
            combined,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Purge all offline values",
            combined,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sensitive path",
            combined,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableWindowsVerificationKeepsApplicationLockedAndFailClosed()
    {
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsUnlocked);
        Assert.False(viewModel.IsApplicationReady);
        Assert.Contains("unavailable", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Hello", viewModel.UnlockRecovery, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UserVerificationResult.Canceled, "canceled")]
    [InlineData(UserVerificationResult.NotConfigured, "not configured")]
    [InlineData(UserVerificationResult.DisabledByPolicy, "policy")]
    [InlineData(UserVerificationResult.Unavailable, "unavailable")]
    [InlineData(UserVerificationResult.RemoteSessionUnavailable, "Remote Desktop")]
    [InlineData(UserVerificationResult.RemoteCredentialUnavailable, "remote")]
    [InlineData(UserVerificationResult.RemoteCredentialFailed, "current Windows account")]
    [InlineData(UserVerificationResult.Failed, "failed")]
    public async Task IncompleteWindowsVerificationKeepsApplicationLocked(
        UserVerificationResult result,
        string expectedStatus)
    {
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new FixedVerificationService(result),
            null!,
            null!);

        await viewModel.InitializeAsync();

        Assert.False(viewModel.IsUnlocked);
        Assert.False(viewModel.IsApplicationReady);
        Assert.Contains(expectedStatus, viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(viewModel.UnlockRecovery);
    }

    [Fact]
    public async Task FirstRunOpensIdentitySetupAfterLocalUnlock()
    {
        var repository = new EmptyRepository();
        var viewModel = new MainViewModel(
            repository,
            null!,
            null!,
            new SearchService(repository, new TestClock()),
            null!,
            new WorkspaceService(repository),
            null!,
            new AppSettingsStore(
                Path.Combine(_directory, "settings.json")),
            new FixedVerificationService(
                UserVerificationResult.Verified),
            null!,
            new UnsupportedManagedIdentityDetector());
        var identityTypeResetCount = 0;
        viewModel.IdentityTypes.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Reset)
            {
                identityTypeResetCount++;
            }
        };

        await viewModel.InitializeAsync();

        Assert.True(viewModel.IsUnlocked);
        Assert.True(viewModel.IsApplicationReady);
        Assert.True(viewModel.IsFirstRun);
        Assert.Equal(1, viewModel.SelectedMainTabIndex);
        Assert.Equal(
            "Continue to Microsoft sign-in",
            viewModel.ConnectIdentityActionText);
        Assert.Equal(
            IdentityType.InteractiveUser,
            viewModel.SelectedIdentityType);
        Assert.Contains(
            viewModel.SelectedIdentityType,
            viewModel.IdentityTypes);
        Assert.Equal(0, identityTypeResetCount);
        Assert.Contains(
            "Local unlock complete",
            viewModel.StatusText,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        IdentityType.InteractiveUser,
        "Continue to Microsoft sign-in")]
    [InlineData(
        IdentityType.ManagedIdentity,
        "Verify and connect managed identity")]
    [InlineData(
        IdentityType.ServicePrincipal,
        "Verify and connect certificate identity")]
    [InlineData(
        IdentityType.FederatedServicePrincipal,
        "Verify and connect federated identity")]
    public void ConnectionActionNamesTheSelectedAuthenticationMethod(
        IdentityType identityType,
        string expectedAction)
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedIdentityType = identityType;

        Assert.Equal(expectedAction, viewModel.ConnectIdentityActionText);
    }

    [Fact]
    public void IdentityActionsTrackSelectionAndBusyState()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedIdentityCacheCommand.CanExecute(null));
        Assert.False(viewModel.RotateSelectedCredentialCommand.CanExecute(null));

        viewModel.SelectedIdentity = new ConnectedIdentity(
            Guid.NewGuid(),
            ProductIdentity.DefaultClientId,
            "account",
            "user@example.invalid",
            "Test identity",
            "tenant",
            AuthenticationState.Ready,
            DateTimeOffset.UtcNow);

        Assert.True(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.True(viewModel.RemoveIdentityCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedIdentityCacheCommand.CanExecute(null));
        Assert.False(viewModel.RotateSelectedCredentialCommand.CanExecute(null));

        viewModel.SelectedIdentity = viewModel.SelectedIdentity with
        {
            Type = IdentityType.FederatedServicePrincipal,
        };
        viewModel.ReplacementCredentialData = "C:\\projected\\token";

        Assert.True(viewModel.RotateSelectedCredentialCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedIdentityCacheCommand.CanExecute(null));
        Assert.False(viewModel.RotateSelectedCredentialCommand.CanExecute(null));

        viewModel.IsBusy = false;
        viewModel.SelectedIdentity = null;

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveIdentityCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedIdentityCacheCommand.CanExecute(null));
        Assert.False(viewModel.RotateSelectedCredentialCommand.CanExecute(null));
    }

    [Fact]
    public void RevokedIdentityCannotSynchronizeButCanBeRemovedOrRecovered()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedIdentity = CreateIdentity() with
        {
            IsEnabled = false,
            AuthenticationState = AuthenticationState.Revoked,
        };

        Assert.False(viewModel.SynchronizeCommand.CanExecute(null));
        Assert.True(viewModel.RemoveIdentityCommand.CanExecute(null));
        Assert.True(viewModel.ReauthenticateIdentityCommand.CanExecute(null));
        Assert.True(viewModel.EnableIdentityCommand.CanExecute(null));
    }

    [Fact]
    public async Task WorkloadAdministrationRequiresInteractiveIdentityAndProducesPreviewOnlyPlan()
    {
        var administration = new FakeWorkloadAdministrationService();
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!,
            administration);
        viewModel.SelectedIdentity = CreateIdentity();
        viewModel.AdministrationSubscriptionId = "11111111-1111-1111-1111-111111111111";
        viewModel.AdministrationResourceGroup = "rg-automation";
        viewModel.AdministrationIdentityName = "vault-prospector-reader";

        Assert.True(viewModel.DiscoverManagedIdentitiesCommand.CanExecute(null));
        Assert.True(viewModel.DiscoverServicePrincipalsCommand.CanExecute(null));
        Assert.True(viewModel.PreviewManagedIdentityCommand.CanExecute(null));
        Assert.True(viewModel.PreviewServicePrincipalCommand.CanExecute(null));

        await viewModel.PreviewManagedIdentityCommand.ExecuteAsync(null);

        Assert.Contains("PREVIEW ONLY", viewModel.AdministrationPlanText, StringComparison.Ordinal);
        Assert.Contains("performs mutations: False", viewModel.AdministrationPlanText, StringComparison.Ordinal);

        viewModel.SelectedIdentity = CreateIdentity() with
        {
            Type = IdentityType.ServicePrincipal,
        };

        Assert.False(viewModel.DiscoverManagedIdentitiesCommand.CanExecute(null));
        Assert.False(viewModel.DiscoverServicePrincipalsCommand.CanExecute(null));
        Assert.False(viewModel.PreviewManagedIdentityCommand.CanExecute(null));
    }

    [Fact]
    public async Task WorkloadPermissionAssessmentRequiresSelectionAndExactVaultThenReplacesEvidence()
    {
        var administration = new FakeWorkloadAdministrationService();
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!,
            administration)
        {
            SelectedIdentity = CreateIdentity(),
        };
        var candidate = new WorkloadIdentityCandidate(
            "User-assigned managed identity",
            "tenant",
            "11111111-1111-1111-1111-111111111111",
            "rg",
            "reader",
            "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/reader",
            "22222222-2222-2222-2222-222222222222",
            "33333333-3333-3333-3333-333333333333",
            "eastus",
            true,
            new WorkloadPermissionAssessment(
                "Confirmed",
                "Not proven",
                "Not proven",
                "Not proven",
                "Not proven"));
        var candidateRow = new WorkloadIdentityCandidateRow(candidate);
        viewModel.WorkloadIdentityCandidates.Add(candidateRow);
        viewModel.SelectedWorkloadIdentityCandidate = candidateRow;

        Assert.False(
            viewModel.AssessWorkloadIdentityPermissionsCommand.CanExecute(null));

        viewModel.AdministrationVaultResourceId =
            "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/example";

        Assert.True(
            viewModel.AssessWorkloadIdentityPermissionsCommand.CanExecute(null));
        await viewModel.AssessWorkloadIdentityPermissionsCommand.ExecuteAsync(null);

        Assert.NotNull(administration.AssessedCandidate);
        Assert.Single(viewModel.WorkloadIdentityCandidates);
        Assert.Same(
            viewModel.WorkloadIdentityCandidates[0],
            viewModel.SelectedWorkloadIdentityCandidate);
        Assert.Contains(
            "Assessed at",
            viewModel.SelectedWorkloadIdentityCandidate.PermissionSummary,
            StringComparison.Ordinal);
        Assert.Contains("No Azure resources or values were changed", viewModel.StatusText);
    }

    [Fact]
    public async Task WorkloadIdentityFilterIsDeterministicAndSearchable()
    {
        var candidates = new[]
        {
            WorkloadCandidate(
                "Zulu automation",
                "33333333-3333-3333-3333-333333333333"),
            WorkloadCandidate(
                "Alpha deployment",
                "11111111-1111-1111-1111-111111111111"),
            WorkloadCandidate(
                "Beta reporting",
                "22222222-2222-2222-2222-222222222222"),
        };
        var administration =
            new FakeWorkloadAdministrationService
            {
                ServicePrincipals = candidates,
            };
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!,
            administration)
        {
            SelectedIdentity = CreateIdentity(),
        };

        await viewModel.DiscoverServicePrincipalsCommand
            .ExecuteAsync(null);

        Assert.Equal(
            ["Alpha deployment", "Beta reporting", "Zulu automation"],
            viewModel.WorkloadIdentityCandidates
                .Select(row => row.DisplayName)
                .ToArray());

        viewModel.WorkloadIdentitySearchText = "report";

        var candidate = Assert.Single(
            viewModel.WorkloadIdentityCandidates);
        Assert.Equal(
            "Beta reporting",
            candidate.DisplayName);
        Assert.Contains(
            "1 of 3",
            viewModel.WorkloadIdentityFilterStatus,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InterruptedRotationRecoveryRunsOnlyAfterLocalVerification()
    {
        var engine = new FakeRotationEngine
        {
            RecoveryException = new LocalDataIntegrityException(
                "test rotation journal failure"),
        };
        var lockedViewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new FixedVerificationService(
                UserVerificationResult.Canceled),
            null!,
            null!,
            null,
            engine);

        await lockedViewModel.InitializeAsync();

        Assert.Equal(0, engine.RecoveryCalls);
        Assert.False(lockedViewModel.IsUnlocked);

        var verifiedViewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new FixedVerificationService(
                UserVerificationResult.Verified),
            null!,
            null!,
            null,
            engine);

        await verifiedViewModel.InitializeAsync();

        Assert.Equal(1, engine.RecoveryCalls);
        Assert.True(verifiedViewModel.IsLocalDataBlocked);
        Assert.False(verifiedViewModel.IsApplicationReady);
    }

    [Fact]
    public void ResultActionsRequireCompatibleSelectionAndCachePolicy()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Key);

        Assert.True(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.CopyCommand.CanExecute(null));
        Assert.False(viewModel.OpenOfflineCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedCachedValueCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Secret);

        Assert.True(viewModel.RevealCommand.CanExecute(null));
        Assert.True(viewModel.CopyCommand.CanExecute(null));
        Assert.True(viewModel.OpenOfflineCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedCachedValueCommand.CanExecute(null));
        Assert.False(viewModel.CacheSelectedCommand.CanExecute(null));

        viewModel.OfflineCacheEnabled = true;

        Assert.True(viewModel.CacheSelectedCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(null));
        Assert.False(viewModel.RevealCommand.CanExecute(null));
        Assert.False(viewModel.CacheSelectedCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedVaultCacheCommand.CanExecute(null));
    }

    [Fact]
    public void WorkspaceActionsRequireTheirExactPrerequisites()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.WorkspaceName = "  ";
        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        viewModel.WorkspaceName = "Operations";
        Assert.True(viewModel.CreateWorkspaceCommand.CanExecute(null));

        viewModel.SelectedWorkspace = new Workspace(Guid.NewGuid(), "Operations", string.Empty, 0);

        Assert.True(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.True(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.SelectedResult = CreateResult(VaultObjectType.Certificate);
        Assert.True(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));

        viewModel.SelectedIdentity = CreateIdentity();
        Assert.True(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.IsBusy = true;

        Assert.False(viewModel.CreateWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));

        viewModel.IsBusy = false;
        viewModel.SelectedWorkspace = null;

        Assert.False(viewModel.RemoveWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.PurgeSelectedWorkspaceCacheCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedVaultToWorkspaceCommand.CanExecute(null));
        Assert.False(viewModel.AddSelectedIdentityToWorkspaceCommand.CanExecute(null));
    }

    [Fact]
    public void GeneralOperationsDisableWhileBusyAndEmptySelectionFiltersReset()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.AddIdentityCommand.CanExecute(null));
        Assert.True(viewModel.SearchCommand.CanExecute(null));
        Assert.True(viewModel.SaveSettingsCommand.CanExecute(null));
        Assert.True(viewModel.PurgeAllCachedValuesCommand.CanExecute(null));

        viewModel.SelectedIdentity = CreateIdentity();
        viewModel.SelectedWorkspace = new Workspace(Guid.NewGuid(), "Operations", string.Empty, 0);
        Assert.True(viewModel.HasSelectedIdentity);
        Assert.True(viewModel.HasSelectedWorkspace);
        viewModel.FilterSelectedIdentity = true;
        viewModel.FilterSelectedWorkspace = true;

        viewModel.SelectedIdentity = null;
        viewModel.SelectedWorkspace = null;

        Assert.False(viewModel.HasSelectedIdentity);
        Assert.False(viewModel.HasSelectedWorkspace);
        Assert.False(viewModel.FilterSelectedIdentity);
        Assert.False(viewModel.FilterSelectedWorkspace);

        viewModel.IsBusy = true;

        Assert.False(viewModel.AddIdentityCommand.CanExecute(null));
        Assert.False(viewModel.SearchCommand.CanExecute(null));
        Assert.False(viewModel.SaveSettingsCommand.CanExecute(null));
        Assert.False(viewModel.PurgeAllCachedValuesCommand.CanExecute(null));
    }

    [Fact]
    public void BackgroundingImmediatelyLocksAndHidesSensitivePresentation()
    {
        var revealSession = new TrackingRevealVerificationSession();
        var viewModel = CreateViewModel(revealSession);
        viewModel.IsUnlocked = true;
        viewModel.IsApplicationReady = true;
        viewModel.SecretPreview = "sensitive-value";

        viewModel.LockForBackground();

        Assert.False(viewModel.IsUnlocked);
        Assert.False(viewModel.IsApplicationReady);
        Assert.Equal("Secret hidden.", viewModel.SecretPreview);
        Assert.Contains("Locked", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, revealSession.InvalidationCount);
    }

    [Fact]
    public void WindowsSecurityBoundaryImmediatelyLocksAndHidesSensitivePresentation()
    {
        var revealSession = new TrackingRevealVerificationSession();
        var viewModel = CreateViewModel(revealSession);
        viewModel.IsUnlocked = true;
        viewModel.IsApplicationReady = true;
        viewModel.IsCloseChoiceVisible = true;
        viewModel.SecretPreview = "sensitive-value";

        viewModel.LockForSystemBoundary();

        Assert.False(viewModel.IsUnlocked);
        Assert.False(viewModel.IsApplicationReady);
        Assert.False(viewModel.IsCloseChoiceVisible);
        Assert.Equal("Secret hidden.", viewModel.SecretPreview);
        Assert.Contains("Windows", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(1, revealSession.InvalidationCount);
    }

    [Fact]
    public void RevealGraceInvalidatesOnSettingIdentityWorkspaceAndManualLock()
    {
        var revealSession = new TrackingRevealVerificationSession();
        var viewModel = CreateViewModel(revealSession);

        viewModel.SelectedRevealVerificationGrace =
            RevealVerificationGraceOption.All.Single(
                option => option.Seconds == 60);
        viewModel.SelectedIdentity = CreateIdentity();
        viewModel.SelectedWorkspace = new Workspace(
            Guid.NewGuid(),
            "Operations",
            string.Empty,
            0);
        viewModel.IsUnlocked = true;
        viewModel.IsApplicationReady = true;
        viewModel.SecretPreview = "sensitive-value";
        viewModel.LockNowCommand.Execute(null);

        Assert.Equal(4, revealSession.InvalidationCount);
        Assert.False(viewModel.IsUnlocked);
        Assert.Equal("Secret hidden.", viewModel.SecretPreview);
    }

    [Fact]
    public void ExplicitCloseChoiceCommandsRaiseOnlyTheSelectedLifecycleRequest()
    {
        var viewModel = CreateViewModel();
        var exitRequests = 0;
        var backgroundRequests = 0;
        viewModel.ExitRequested += (_, _) => exitRequests++;
        viewModel.ContinueInBackgroundRequested += (_, _) => backgroundRequests++;

        viewModel.RequestCloseChoice();
        Assert.True(viewModel.IsCloseChoiceVisible);
        viewModel.CancelCloseCommand.Execute(null);
        Assert.False(viewModel.IsCloseChoiceVisible);

        viewModel.RequestCloseChoice();
        viewModel.ContinueInBackgroundCommand.Execute(null);
        Assert.Equal(0, exitRequests);
        Assert.Equal(1, backgroundRequests);
        Assert.False(viewModel.IsCloseChoiceVisible);

        viewModel.RequestExitCommand.Execute(null);
        Assert.Equal(1, exitRequests);
        Assert.Equal(1, backgroundRequests);
    }

    [Fact]
    public async Task OptInBackgroundSyncUsesMetadataDiscoveryOnlyForReadyIdentity()
    {
        var identity = CreateIdentity();
        var subscription = new SubscriptionAccess(Guid.NewGuid(), Guid.NewGuid(), "subscription", "Subscription", "Enabled", true, DateTimeOffset.UtcNow);
        var repository = new SubscriptionRepository(identity, subscription);
        var provider = new BackgroundProvider();
        var viewModel = new MainViewModel(
            repository,
            null!,
            new SynchronizationService(provider, repository, new TestClock(), new TestDiagnostics()),
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!)
        {
            SelectedIdentity = identity,
            BackgroundMetadataSyncEnabled = true,
        };

        await viewModel.BackgroundSynchronizeOnceAsync();

        Assert.Equal(1, provider.DiscoveryCalls);
        Assert.Equal(0, provider.RetrievalCalls);
    }

    [Fact]
    public async Task SubscriptionScopeCommandsPersistExplicitInclusionChoice()
    {
        var identity = CreateIdentity();
        var subscription = new SubscriptionAccess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "Production",
            "Enabled",
            true,
            DateTimeOffset.UtcNow);
        var repository = new SubscriptionRepository(identity, subscription);
        var viewModel = new MainViewModel(
            repository,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!)
        {
            SelectedIdentity = identity,
            SelectedSubscription = new SubscriptionSelectionRow(subscription),
        };

        Assert.True(viewModel.ExcludeSubscriptionCommand.CanExecute(null));
        Assert.False(viewModel.IncludeSubscriptionCommand.CanExecute(null));

        await viewModel.ExcludeSubscriptionCommand.ExecuteAsync(null);

        Assert.Equal((subscription.Id, false), repository.LastSelection);
        Assert.NotNull(viewModel.SelectedSubscription);
        Assert.False(viewModel.SelectedSubscription.IsSelected);
        Assert.True(viewModel.IncludeSubscriptionCommand.CanExecute(null));
    }

    [Fact]
    public async Task VaultScopeCommandsPersistExplicitInclusionChoice()
    {
        var identity = CreateIdentity();
        var subscription = new SubscriptionAccess(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "11111111-1111-1111-1111-111111111111",
            "Production",
            "Enabled",
            true,
            DateTimeOffset.UtcNow);
        var vault = new VaultResource(
            Guid.NewGuid(),
            "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/one",
            "one",
            "tenant",
            subscription.SubscriptionId,
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://one.vault.azure.net/"),
            DateTimeOffset.UtcNow);
        var access = new VaultAccess(Guid.NewGuid(), vault.Id, identity.Id, "tenant", "Visible", DateTimeOffset.UtcNow, null, 0);
        var repository = new SubscriptionRepository(identity, subscription)
        {
            VaultSummary = new VaultAccessSummary(vault, access, identity.DisplayName, "Tenant"),
        };
        var viewModel = new MainViewModel(
            repository,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!)
        {
            SelectedIdentity = identity,
            SelectedVaultAccess = new VaultAccessRow(repository.VaultSummary),
        };

        Assert.True(viewModel.ExcludeVaultCommand.CanExecute(null));
        await viewModel.ExcludeVaultCommand.ExecuteAsync(null);

        Assert.Equal((access.Id, false), repository.LastVaultSelection);
        Assert.NotNull(viewModel.SelectedVaultAccess);
        Assert.False(viewModel.SelectedVaultAccess.IsSelected);
        Assert.True(viewModel.IncludeVaultCommand.CanExecute(null));
    }

    [Fact]
    public async Task WorkspaceAcceptsTenantAndSubscriptionLinksAndPersistsSecurePolicy()
    {
        var identity = CreateIdentity();
        var tenant = new TenantAccess(Guid.NewGuid(), identity.Id, "tenant-id", "Tenant", "Home", DateTimeOffset.UtcNow, "Available");
        var subscription = new SubscriptionAccess(Guid.NewGuid(), tenant.Id, "subscription-id", "Subscription", "Enabled", true, DateTimeOffset.UtcNow);
        var repository = new SubscriptionRepository(identity, subscription)
        {
            Tenants = [tenant],
        };
        var workspace = new Workspace(Guid.NewGuid(), "Operations", string.Empty, 0, CachePolicy.SecureDefault);
        repository.Workspaces = [workspace];
        var viewModel = new MainViewModel(
            repository,
            null!,
            null!,
            new SearchService(repository, new TestClock()),
            null!,
            new WorkspaceService(repository),
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!)
        {
            SelectedIdentity = identity,
            SelectedTenant = tenant,
            SelectedSubscription = new SubscriptionSelectionRow(subscription),
            SelectedWorkspace = workspace,
        };

        Assert.True(viewModel.AddSelectedTenantToWorkspaceCommand.CanExecute(null));
        Assert.True(viewModel.AddSelectedSubscriptionToWorkspaceCommand.CanExecute(null));
        await viewModel.AddSelectedTenantToWorkspaceCommand.ExecuteAsync(null);
        await viewModel.AddSelectedSubscriptionToWorkspaceCommand.ExecuteAsync(null);

        Assert.Contains(repository.AddedLinks, link =>
            link.ResourceType == ResourceLinkType.Tenant && link.ResourceId == tenant.TenantId);
        Assert.Contains(repository.AddedLinks, link =>
            link.ResourceType == ResourceLinkType.Subscription && link.ResourceId == subscription.SubscriptionId);

        viewModel.WorkspaceCacheEnabled = true;
        viewModel.WorkspaceMaximumCacheHours = 24;
        viewModel.WorkspaceAllowClipboard = false;
        await viewModel.SaveWorkspacePolicyCommand.ExecuteAsync(null);

        Assert.NotNull(repository.SavedWorkspace?.CachePolicyOverride);
        Assert.True(repository.SavedWorkspace.CachePolicyOverride.IsEnabled);
        Assert.Equal(TimeSpan.FromHours(24), repository.SavedWorkspace.CachePolicyOverride.MaximumLifetime);
        Assert.True(repository.SavedWorkspace.CachePolicyOverride.RequireLocalUnlock);
        Assert.False(repository.SavedWorkspace.CachePolicyOverride.AllowClipboard);
    }

    [Fact]
    public async Task RecoveryArchiveDeletionRequiresSelectionPhraseAndVerification()
    {
        var archive = new LocalRecoveryArchive(
            "20260723-120102003-11111111111111111111111111111111",
            LocalRecoveryArchiveKind.Reset,
            DateTimeOffset.Parse(
                "2026-07-23T12:01:02.003Z",
                System.Globalization.CultureInfo.InvariantCulture),
            42);
        var store = new FakeRecoveryArchiveStore
        {
            Archives = [archive],
        };
        var verification = new FixedVerificationService(
            UserVerificationResult.Verified);
        var service = new LocalRecoveryArchiveService(
            verification,
            store,
            new TestDiagnostics());
        var viewModel = new MainViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            verification,
            null!,
            null!,
            localRecoveryArchiveService: service);

        await viewModel.RefreshRecoveryArchivesCommand.ExecuteAsync(null);
        viewModel.SelectedRecoveryArchive =
            Assert.Single(viewModel.RecoveryArchives);

        Assert.False(
            viewModel.DeleteSelectedRecoveryArchiveCommand
                .CanExecute(null));

        viewModel.RecoveryArchiveDeleteConfirmation =
            LocalRecoveryArchiveService.ConfirmationPhrase;

        Assert.True(
            viewModel.DeleteSelectedRecoveryArchiveCommand
                .CanExecute(null));

        await viewModel.DeleteSelectedRecoveryArchiveCommand
            .ExecuteAsync(null);

        Assert.Equal(archive.Id, store.DeletedArchiveId);
        Assert.Empty(viewModel.RecoveryArchives);
        Assert.Null(viewModel.SelectedRecoveryArchive);
        Assert.Empty(
            viewModel.RecoveryArchiveDeleteConfirmation);
    }

    private static MainViewModel CreateViewModel(
        IRevealVerificationSession? revealVerificationSession = null) =>
        new(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new UnavailableVerificationService(),
            null!,
            null!,
            revealVerificationSession:
                revealVerificationSession);

    private sealed class UnavailableVerificationService : IUserVerificationService
    {
        public bool IsAvailable => false;

        public Task<UserVerificationResult> VerifyAsync(string reason, CancellationToken cancellationToken) =>
            Task.FromResult(UserVerificationResult.Unavailable);
    }

    private sealed class FixedVerificationService(UserVerificationResult result) : IUserVerificationService
    {
        public bool IsAvailable => true;

        public Task<UserVerificationResult> VerifyAsync(string reason, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class TrackingRevealVerificationSession :
        IRevealVerificationSession
    {
        public int InvalidationCount { get; private set; }

        public Task<bool> EnsureVerifiedAsync(
            TimeSpan requestedGracePeriod,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public void Invalidate() => InvalidationCount++;
    }

    private sealed class SubscriptionRepository(
        ConnectedIdentity identity,
        SubscriptionAccess subscription) : IMetadataRepository
    {
        private SubscriptionAccess _subscription = subscription;
        public (Guid Id, bool IsSelected)? LastSelection { get; private set; }
        public VaultAccessSummary? VaultSummary { get; set; }
        public (Guid Id, bool IsSelected)? LastVaultSelection { get; private set; }
        public IReadOnlyList<TenantAccess> Tenants { get; set; } = [];
        public IReadOnlyList<Workspace> Workspaces { get; set; } = [];
        public Workspace? SavedWorkspace { get; private set; }
        public List<WorkspaceResourceLink> AddedLinks { get; } = [];
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>([identity]);
        public Task<ConnectedIdentity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ConnectedIdentity?>(id == identity.Id ? identity : null);
        public Task UpsertIdentityAsync(ConnectedIdentity value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveIdentityAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult(Tenants);
        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SubscriptionAccess>>(identityId == identity.Id ? [_subscription] : []);
        public Task SetSubscriptionSelectedAsync(Guid subscriptionAccessId, bool isSelected, CancellationToken cancellationToken)
        {
            LastSelection = (subscriptionAccessId, isSelected);
            _subscription = _subscription with { IsSelected = isSelected };
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VaultAccessSummary>>(
                identityId == identity.Id && VaultSummary is not null ? [VaultSummary] : []);
        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(Guid identityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>(
                identityId == identity.Id && VaultSummary is not null
                    ? [VaultSummary.Vault.Id]
                    : []);
        public Task SetVaultSelectedAsync(Guid vaultAccessId, bool isSelected, CancellationToken cancellationToken)
        {
            LastVaultSelection = (vaultAccessId, isSelected);
            if (VaultSummary is not null)
                VaultSummary = VaultSummary with { Access = VaultSummary.Access with { IsSelected = isSelected } };
            return Task.CompletedTask;
        }
        public Task ApplyDiscoveryAsync(Guid identityId, DiscoverySnapshot snapshot, SyncRun run, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);
        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?> ResolveItemAsync(Guid itemId, CancellationToken cancellationToken) =>
            Task.FromResult<(VaultItem, VaultResource, ConnectedIdentity)?>(null);
        public Task RecordAccessAsync(Guid itemId, DateTimeOffset accessedAt, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid itemId, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Workspaces);
        public Task UpsertWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            SavedWorkspace = workspace;
            Workspaces = [workspace];
            return Task.CompletedTask;
        }
        public Task RemoveWorkspaceAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddWorkspaceLinkAsync(WorkspaceResourceLink link, CancellationToken cancellationToken)
        {
            AddedLinks.Add(link);
            return Task.CompletedTask;
        }
        public Task RemoveWorkspaceLinkAsync(Guid workspaceId, ResourceLinkType resourceType, string resourceId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EmptyRepository : IMetadataRepository
    {
        public List<ConnectedIdentity> Identities { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ConnectedIdentity>> GetIdentitiesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConnectedIdentity>>(
                Identities.ToArray());

        public Task<ConnectedIdentity?> GetIdentityAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Identities.FirstOrDefault(identity => identity.Id == id));

        public Task UpsertIdentityAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken)
        {
            var index = Identities.FindIndex(existing => existing.Id == identity.Id);
            if (index < 0)
                Identities.Add(identity);
            else
                Identities[index] = identity;
            return Task.CompletedTask;
        }

        public Task RemoveIdentityAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            Identities.RemoveAll(identity => identity.Id == id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TenantAccess>> GetTenantsAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantAccess>>([]);

        public Task<IReadOnlyList<SubscriptionAccess>> GetSubscriptionsAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SubscriptionAccess>>([]);

        public Task SetSubscriptionSelectedAsync(
            Guid subscriptionAccessId,
            bool isSelected,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<VaultAccessSummary>> GetVaultAccessSummariesAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VaultAccessSummary>>([]);

        public Task<IReadOnlyList<Guid>> GetVaultIdsForIdentityAsync(
            Guid identityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task SetVaultSelectedAsync(
            Guid vaultAccessId,
            bool isSelected,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ApplyDiscoveryAsync(
            Guid identityId,
            DiscoverySnapshot snapshot,
            SyncRun run,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            SearchRequest request,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);

        public Task<(VaultItem Item, VaultResource Vault, ConnectedIdentity Identity)?>
            ResolveItemAsync(
                Guid itemId,
                CancellationToken cancellationToken) =>
            Task.FromResult<
                (VaultItem, VaultResource, ConnectedIdentity)?>(null);

        public Task RecordAccessAsync(
            Guid itemId,
            DateTimeOffset accessedAt,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SetFavoriteAsync(
            Guid itemId,
            bool isFavorite,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Workspace>>([]);

        public Task UpsertWorkspaceAsync(
            Workspace workspace,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveWorkspaceAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddWorkspaceLinkAsync(
            WorkspaceResourceLink link,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveWorkspaceLinkAsync(
            Guid workspaceId,
            ResourceLinkType resourceType,
            string resourceId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SequentialIdentityProvider : IIdentityProvider
    {
        public Task<ConnectedIdentity> SignInAsync(
            string clientId,
            string displayName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectedIdentity(
                Guid.NewGuid(),
                clientId,
                Guid.NewGuid().ToString("D"),
                $"{displayName.Replace(' ', '.').ToLowerInvariant()}@example.invalid",
                displayName,
                "11111111-1111-1111-1111-111111111111",
                AuthenticationState.Ready,
                DateTimeOffset.UtcNow));

        public Task<ConnectedIdentity> ReauthenticateAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.FromResult(identity);

        public Task<ConnectedIdentity> AuthorizeDirectoryReadAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.FromResult(identity);

        public Task RemoveAsync(
            ConnectedIdentity identity,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnsupportedManagedIdentityDetector :
        IManagedIdentityEnvironmentDetector
    {
        public Task<ManagedIdentityEnvironmentStatus> DetectAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new ManagedIdentityEnvironmentStatus(
                    false,
                    "Managed identity is unavailable in this test."));
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-07-23T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class FakeWorkloadAdministrationService : IWorkloadIdentityAdministrationService
    {
        public WorkloadIdentityCandidate? AssessedCandidate { get; private set; }
        public IReadOnlyList<WorkloadIdentityCandidate>
            ServicePrincipals { get; init; } = [];

        public Task<IReadOnlyList<WorkloadIdentityCandidate>> ListManagedIdentitiesAsync(
            ConnectedIdentity administrator,
            string subscriptionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkloadIdentityCandidate>>([]);

        public Task<IReadOnlyList<WorkloadIdentityCandidate>> ListServicePrincipalsAsync(
            ConnectedIdentity administrator,
            CancellationToken cancellationToken) =>
            Task.FromResult(ServicePrincipals);

        public Task<WorkloadIdentityCandidate> AssessPermissionsAsync(
            ConnectedIdentity administrator,
            WorkloadIdentityCandidate candidate,
            string keyVaultResourceId,
            CancellationToken cancellationToken)
        {
            AssessedCandidate = candidate with
            {
                Permissions = candidate.Permissions with
                {
                    KeyVaultDataAccess =
                        $"Assessed at {keyVaultResourceId}",
                },
            };
            return Task.FromResult(AssessedCandidate);
        }

        public WorkloadIdentityProvisioningPlan BuildManagedIdentityDryRun(
            string tenantId,
            string subscriptionId,
            string resourceGroupName,
            string identityName,
            string? keyVaultResourceId = null,
            string? keyVaultRoleDefinitionId = null) =>
            new(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "UserAssignedManagedIdentity",
                tenantId,
                subscriptionId,
                resourceGroupName,
                identityName,
                [
                    new PlannedAzureOperation(
                        "Create",
                        "Microsoft.ManagedIdentity/userAssignedIdentities",
                        $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}",
                        "Preview"),
                ]);

        public WorkloadIdentityProvisioningPlan BuildServicePrincipalDryRun(
            string tenantId,
            string identityName,
            string? keyVaultResourceId = null,
            string? keyVaultRoleDefinitionId = null) =>
            new(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "ServicePrincipal",
                tenantId,
                string.Empty,
                string.Empty,
                identityName,
                []);
    }

    private static WorkloadIdentityCandidate WorkloadCandidate(
        string displayName,
        string principalId) =>
        new(
            "Service principal (Application)",
            "tenant",
            string.Empty,
            string.Empty,
            displayName,
            $"/tenants/tenant/servicePrincipals/{principalId}",
            Guid.NewGuid().ToString("D"),
            principalId,
            string.Empty,
            true,
            new WorkloadPermissionAssessment(
                "Confirmed",
                "Not proven",
                "Not proven",
                "Not proven",
                "Not proven"));

    private sealed class FakeRotationEngine :
        ILocalEncryptionRotationEngine
    {
        public int RecoveryCalls { get; private set; }
        public Exception? RecoveryException { get; init; }

        public Task<LocalEncryptionRotationResult> RotateAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LocalEncryptionRecoveryResult> RecoverIfNeededAsync(
            CancellationToken cancellationToken)
        {
            RecoveryCalls++;
            return RecoveryException is null
                ? Task.FromResult(
                    new LocalEncryptionRecoveryResult(false))
                : Task.FromException<LocalEncryptionRecoveryResult>(
                    RecoveryException);
        }
    }

    private sealed class BackgroundProvider : IVaultProvider
    {
        public int DiscoveryCalls { get; private set; }
        public int RetrievalCalls { get; private set; }
        public Task<DiscoverySnapshot> DiscoverAsync(
            ConnectedIdentity identity,
            IReadOnlyList<string> excludedSubscriptions,
            IReadOnlyList<string> excludedVaultResourceIds,
            CancellationToken cancellationToken)
        {
            DiscoveryCalls++;
            return Task.FromResult(new DiscoverySnapshot([], [], [], [], [], []));
        }

        public Task<SensitiveValue> RetrieveSecretAsync(
            ConnectedIdentity identity,
            VaultResource vault,
            VaultItem item,
            CancellationToken cancellationToken)
        {
            RetrievalCalls++;
            return Task.FromResult(new SensitiveValue("should-not-be-called"));
        }
    }

    private sealed class TestDiagnostics : IDiagnosticSink
    {
        public void Information(string eventName, IReadOnlyDictionary<string, object?> fields) { }
        public void WriteError(string eventName, Exception exception, IReadOnlyDictionary<string, object?> fields) { }
    }

    private sealed class FakeRecoveryArchiveStore :
        ILocalRecoveryArchiveStore
    {
        public IReadOnlyList<LocalRecoveryArchive> Archives
        {
            get;
            set;
        } = [];
        public string? DeletedArchiveId { get; private set; }

        public Task<IReadOnlyList<LocalRecoveryArchive>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Archives);

        public Task DeleteAsync(
            string archiveId,
            CancellationToken cancellationToken)
        {
            DeletedArchiveId = archiveId;
            Archives = Archives
                .Where(archive => archive.Id != archiveId)
                .ToArray();
            return Task.CompletedTask;
        }
    }

    private static ConnectedIdentity CreateIdentity() => new(
        Guid.NewGuid(),
        ProductIdentity.DefaultClientId,
        "account",
        "user@example.invalid",
        "Test identity",
        "tenant",
        AuthenticationState.Ready,
        DateTimeOffset.UtcNow);

    private static SearchResultRow CreateResult(VaultObjectType objectType)
    {
        var vaultId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var vault = new VaultResource(
            vaultId,
            $"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/{vaultId:N}",
            "vault",
            "tenant",
            "subscription",
            "rg",
            "eastus",
            new Dictionary<string, string>(),
            new Uri("https://vault.vault.azure.net/"),
            now);
        var item = new VaultItem(
            Guid.NewGuid(),
            vaultId,
            "item",
            objectType,
            true,
            new Dictionary<string, string>(),
            null,
            now,
            now,
            null,
            "version",
            "fingerprint",
            now);
        return new SearchResultRow(new SearchResult(item, vault, "identity", "tenant", false, null, false));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }
}
