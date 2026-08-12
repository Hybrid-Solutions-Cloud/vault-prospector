using VaultProspector.Infrastructure;

namespace VaultProspector.Security.Tests;

public sealed class RedactionTests
{
    private static readonly string[] ProhibitedCanaries =
    [
        "secret-value-canary",
        "access-token-canary",
        "private-key-canary",
        "certificate-payload-canary",
        "decrypted-cache-canary",
        "tenant-id-canary",
        "subscription-id-canary",
        "vault-name-canary",
        "object-name-canary",
        "username-canary",
        "client-credential-canary",
        "http-header-canary",
        "http-body-canary",
        "business-reason-canary",
        "diagnostic-path-canary",
    ];

    [Fact]
    public void DiagnosticSinkDropsSensitiveAndUnknownFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vault-prospector-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new RedactingDiagnosticSink(path);
            sink.Information("test", new Dictionary<string, object?>
            {
                ["identity_id"] = "known-identity",
                ["item_count"] = 4,
                ["error_count"] = "sensitive-count-canary",
                ["status"] = "sensitive-status-canary",
                ["identity_type"] = "sensitive-type-canary",
                ["secret_value"] = "super-secret",
                ["username"] = "user@example.invalid",
                ["vault_name"] = "customer-prod-vault",
            });
            sink.WriteError("failed", new InvalidOperationException("exception includes super-secret"), new Dictionary<string, object?>());

            var log = File.ReadAllText(path);
            Assert.Contains("item_count", log, StringComparison.Ordinal);
            Assert.Contains("\"status\":\"unknown\"", log, StringComparison.Ordinal);
            Assert.Contains("\"identity_type\":\"Unknown\"", log, StringComparison.Ordinal);
            Assert.DoesNotContain("known-identity", log, StringComparison.Ordinal);
            Assert.DoesNotContain("sensitive-count-canary", log, StringComparison.Ordinal);
            Assert.DoesNotContain("sensitive-status-canary", log, StringComparison.Ordinal);
            Assert.DoesNotContain("sensitive-type-canary", log, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret", log, StringComparison.Ordinal);
            Assert.DoesNotContain("user@example.invalid", log, StringComparison.Ordinal);
            Assert.DoesNotContain("customer-prod-vault", log, StringComparison.Ordinal);
            Assert.DoesNotContain("exception includes", log, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void DiagnosticSinkRetainsOnlyApprovedCategoricalValues()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new RedactingDiagnosticSink(path);
            sink.Information(
                "identity_connected",
                new Dictionary<string, object?>
                {
                    ["identity_type"] =
                        "FederatedServicePrincipal",
                    ["status"] = "ready",
                    ["scope_id"] = "scope-canary",
                    ["correlation_id"] = "0123456789abcdef",
                    ["error_category"] = "RequestFailedException",
                });

            var log = File.ReadAllText(path);
            Assert.Contains(
                "FederatedServicePrincipal",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"status\":\"ready\"",
                log,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "scope-canary",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"correlation_id\":\"0123456789ABCDEF\"",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"error_category\":\"azure_request\"",
                log,
                StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void RemoteVerificationDiagnosticsRetainOnlySafeOutcomeCategories()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new RedactingDiagnosticSink(path);
            sink.Information(
                "windows_remote_verification_completed",
                new Dictionary<string, object?>
                {
                    ["status"] = "failed",
                    ["error_category"] = "sid_mismatch",
                    ["username"] = "person@example.invalid",
                    ["reason"] = "private business reason",
                });

            var log = File.ReadAllText(path);
            Assert.Contains(
                "\"event_name\":\"windows_remote_verification_completed\"",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"status\":\"failed\"",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"error_category\":\"sid_mismatch\"",
                log,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "person@example.invalid",
                log,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "private business reason",
                log,
                StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void EveryProhibitedDataClassIsRemovedFromInformationAndErrorEvents()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"vault-prospector-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var fields = ProhibitedCanaries
                .Select(
                    (canary, index) =>
                        new KeyValuePair<string, object?>(
                            $"prohibited_{index}",
                            canary))
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
            fields["identity_id"] =
                string.Join('|', ProhibitedCanaries);
            fields["identity_type"] =
                ProhibitedCanaries[0];
            fields["status"] =
                ProhibitedCanaries[1];
            fields["item_count"] =
                ProhibitedCanaries[2];

            var sink = new RedactingDiagnosticSink(path);
            sink.Information(
                ProhibitedCanaries[3],
                fields);
            sink.WriteError(
                ProhibitedCanaries[4],
                new InvalidOperationException(
                    string.Join('|', ProhibitedCanaries)),
                fields);

            var log = File.ReadAllText(path);
            foreach (var canary in ProhibitedCanaries)
            {
                Assert.DoesNotContain(
                    canary,
                    log,
                    StringComparison.Ordinal);
            }

            Assert.Equal(
                2,
                File.ReadAllLines(path).Length);
            Assert.Equal(
                2,
                log.Split(
                        "\"event_name\":\"application_event\"",
                        StringSplitOptions.None)
                    .Length -
                1);
            Assert.Contains(
                "\"identity_type\":\"Unknown\"",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"status\":\"unknown\"",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"item_count\":null",
                log,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"exception_type\":\"Exception\"",
                log,
                StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
