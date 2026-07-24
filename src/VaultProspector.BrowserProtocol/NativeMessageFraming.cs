using System.Buffers.Binary;

namespace VaultProspector.BrowserProtocol;

public static class NativeMessageFraming
{
    public static async Task<byte[]?> ReadAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var header = new byte[sizeof(uint)];
        var headerBytes = await ReadExactOrEndAsync(input, header, cancellationToken).ConfigureAwait(false);
        if (headerBytes == 0)
            return null;
        if (headerBytes != header.Length)
            throw new BrowserProtocolException("Native message length header is truncated.");

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length is 0 or > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Native message length is outside the protocol limit.");

        var payload = GC.AllocateUninitializedArray<byte>(checked((int)length));
        var payloadBytes = await ReadExactOrEndAsync(input, payload, cancellationToken).ConfigureAwait(false);
        if (payloadBytes != payload.Length)
        {
            Array.Clear(payload);
            throw new BrowserProtocolException("Native message payload is truncated.");
        }

        return payload;
    }

    public static async Task WriteAsync(
        Stream output,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (payload.Length is 0 or > BrowserProtocolConstants.MaximumNativeMessageBytes)
            throw new BrowserProtocolException("Native message size is outside the protocol limit.");

        var header = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(header, checked((uint)payload.Length));
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadExactOrEndAsync(
        Stream input,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var count = await input.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
                break;
            total += count;
        }

        return total;
    }
}
