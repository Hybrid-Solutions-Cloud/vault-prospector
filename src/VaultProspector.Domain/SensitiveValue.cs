using System.Security.Cryptography;

namespace VaultProspector.Domain;

public sealed class SensitiveValue : IDisposable
{
    private char[]? _value;

    public SensitiveValue(ReadOnlySpan<char> value) => _value = value.ToArray();

    public int Length => _value?.Length ?? 0;
    public bool IsDisposed => _value is null;

    public string Reveal()
    {
        ObjectDisposedException.ThrowIf(_value is null, this);
        return new string(_value);
    }

    public string Mask() => new('●', Math.Clamp(Length, 8, 24));

    public void Dispose()
    {
        if (_value is null) return;
        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_value.AsSpan()));
        _value = null;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "[REDACTED]";
}
