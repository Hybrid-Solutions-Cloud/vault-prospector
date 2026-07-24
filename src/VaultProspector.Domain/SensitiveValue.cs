using System.Security.Cryptography;
using System.Text;

namespace VaultProspector.Domain;

public sealed class SensitiveValue : IDisposable
{
    private char[]? _value;

    public SensitiveValue(ReadOnlySpan<char> value) => _value = value.ToArray();

    ~SensitiveValue() => Clear();

    public int Length => _value?.Length ?? 0;
    public bool IsDisposed => _value is null;

    public string Reveal()
    {
        ObjectDisposedException.ThrowIf(_value is null, this);
        return new string(_value);
    }

    public string Mask() => new('●', Math.Clamp(Length, 8, 24));

    public byte[] CopyUtf8Bytes()
    {
        ObjectDisposedException.ThrowIf(_value is null, this);
        return Encoding.UTF8.GetBytes(_value);
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    private void Clear()
    {
        if (_value is null) return;
        CryptographicOperations.ZeroMemory(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_value.AsSpan()));
        _value = null;
    }

    public override string ToString() => "[REDACTED]";
}
