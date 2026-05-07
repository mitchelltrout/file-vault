using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FileVault.Service.Crypto;

public sealed class VaultKey : IDisposable
{
    private readonly byte[] _key;
    private GCHandle _pin;
    private bool _disposed;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualLock(IntPtr lpAddress, nuint dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualUnlock(IntPtr lpAddress, nuint dwSize);

    public VaultKey(byte[] keyBytes)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);
        if (keyBytes.Length != 32)
            throw new ArgumentException("Key must be exactly 32 bytes.", nameof(keyBytes));

        _key = new byte[32];
        keyBytes.AsSpan().CopyTo(_key);
        _pin = GCHandle.Alloc(_key, GCHandleType.Pinned);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            VirtualLock(_pin.AddrOfPinnedObject(), 32); // best-effort; ignore failure
    }

    public ReadOnlySpan<byte> KeyBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _key.AsSpan();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CryptographicOperations.ZeroMemory(_key);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            VirtualUnlock(_pin.AddrOfPinnedObject(), 32);
        _pin.Free();
    }
}
