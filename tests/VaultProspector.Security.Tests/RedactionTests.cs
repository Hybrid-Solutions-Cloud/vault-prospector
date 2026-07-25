using VaultProspector.Infrastructure;

namespace VaultProspector.Security.Tests;

public sealed class RedactionTests
{
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
                ["identity_type"] = "FederatedServicePrincipal",
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
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
