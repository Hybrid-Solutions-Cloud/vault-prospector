using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using VaultProspector.Application;

namespace VaultProspector.Platform;

public sealed class FileSystemSupportBundleService(
    string diagnosticLogPath,
    string supportDirectory,
    string applicationVersion,
    IClock clock) : ISupportBundleService
{
    private const long MaximumDiagnosticLogBytes = 4 * 1024 * 1024;

    public string DiagnosticLogPath { get; } =
        Path.GetFullPath(diagnosticLogPath);

    public async Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destinationDirectory = Path.GetFullPath(supportDirectory);
        Directory.CreateDirectory(destinationDirectory);
        var generatedAt = clock.UtcNow;
        var finalPath = Path.Combine(
            destinationDirectory,
            $"vault-prospector-support-{generatedAt:yyyyMMdd-HHmmss-fff}.zip");
        var temporaryPath = finalPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous))
            {
                using var archive = new ZipArchive(
                    output,
                    ZipArchiveMode.Create,
                    leaveOpen: true);
                await WriteManifestAsync(
                    archive,
                    generatedAt,
                    File.Exists(DiagnosticLogPath),
                    cancellationToken);
                if (File.Exists(DiagnosticLogPath))
                    await CopyBoundedLogAsync(archive, cancellationToken);
            }

            File.Move(temporaryPath, finalPath);
            return finalPath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the original failure.
            }

            throw;
        }
    }

    private async Task WriteManifestAsync(
        ZipArchive archive,
        DateTimeOffset generatedAt,
        bool diagnosticLogIncluded,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            schema = 1,
            generatedAtUtc = generatedAt,
            applicationVersion,
            operatingSystem = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            diagnosticLogIncluded,
            privacy = new
            {
                secretValues = false,
                accessTokens = false,
                userNames = false,
                vaultNames = false,
                objectNames = false,
                automaticUpload = false,
            },
        };
        var entry = archive.CreateEntry(
            "manifest.json",
            CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(
            stream,
            manifest,
            cancellationToken: cancellationToken);
    }

    private async Task CopyBoundedLogAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            DiagnosticLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length > MaximumDiagnosticLogBytes)
            input.Seek(-MaximumDiagnosticLogBytes, SeekOrigin.End);

        var entry = archive.CreateEntry(
            "diagnostics/vault-prospector.log",
            CompressionLevel.Optimal);
        await using var output = entry.Open();
        await input.CopyToAsync(output, cancellationToken);
    }
}
