using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace VaultProspector.Providers.Azure;

internal static class BoundedJsonDocument
{
    internal const int MaximumResponseBytes = 8 * 1024 * 1024;

    public static async Task<JsonDocument> ReadAsync(
        HttpContent content,
        string safeSource,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException(
                $"{safeSource} exceeded the safe response-size limit.");
        }

        await using var source =
            await content.ReadAsStreamAsync(cancellationToken);
        using var buffered = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (true)
            {
                var count = await source.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);
                if (count == 0)
                    break;
                if (buffered.Length + count > MaximumResponseBytes)
                {
                    throw new InvalidDataException(
                        $"{safeSource} exceeded the safe response-size limit.");
                }

                await buffered.WriteAsync(
                    buffer.AsMemory(0, count),
                    cancellationToken);
            }

            buffered.Position = 0;
            try
            {
                return await JsonDocument.ParseAsync(
                    buffered,
                    cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"{safeSource} returned invalid JSON.",
                    exception);
            }
        }
        finally
        {
            if (buffered.TryGetBuffer(out var bufferedContent))
            {
                CryptographicOperations.ZeroMemory(
                    bufferedContent.AsSpan(
                        0,
                        checked((int)buffered.Length)));
            }

            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
