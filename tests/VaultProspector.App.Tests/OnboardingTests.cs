using System.Text.Json;
using VaultProspector.App;

namespace VaultProspector.App.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vault-prospector-app-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task MissingSettingsUseProductRegistrationWithoutCustomMode()
    {
        var store = new AppSettingsStore(Path.Combine(_directory, "settings.json"));

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProductIdentity.DefaultClientId, settings.ClientId);
        Assert.False(settings.UseCustomClientId);
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
    public void WindowsVerificationFailureExplainsThatNoValueWasReleased()
    {
        var error = UserFacingErrorMapper.From(new UnauthorizedAccessException("internal"));

        Assert.Contains("not reveal", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Hello", error.Recovery, StringComparison.OrdinalIgnoreCase);
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

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }
}
