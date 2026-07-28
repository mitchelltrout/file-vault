using FileVault.UI.Ipc;
using LibVLCSharp.Shared;

namespace FileVault.UI.Services;

/// <summary>
/// LibVLC custom media input that reads encrypted vault file ranges via IServiceClient.
/// Maintains a single 1 MB read-ahead slab to amortize IPC round-trips during sequential playback.
/// </summary>
public sealed class VaultMediaInput : MediaInput
{
    private const int SlabSize = 1024 * 1024;

    private readonly IServiceClient _client;
    private readonly string _vaultPath;
    private readonly string _vaultNodePath;
    private readonly long _totalLength;

    private long _position;
    private byte[]? _slab;
    private long _slabStart;
    private int _slabLen;

    public VaultMediaInput(IServiceClient client, string vaultPath, string vaultNodePath, long totalLength)
    {
        _client = client;
        _vaultPath = vaultPath;
        _vaultNodePath = vaultNodePath;
        _totalLength = totalLength;
    }

    public override bool Open(out ulong size)
    {
        size = (ulong)_totalLength;
        _position = 0;
        return true;
    }

    public override int Read(IntPtr buf, uint len)
    {
        if (_position >= _totalLength) return 0;

        var want = (int)Math.Min(len, _totalLength - _position);

        // Refill slab if request is outside current cache.
        if (_slab == null || _position < _slabStart || _position + want > _slabStart + _slabLen)
        {
            var slabStart = _position - (_position % SlabSize);
            var bytes = _client
                .ReadFileRangeAsync(_vaultPath, _vaultNodePath, slabStart, SlabSize)
                .ConfigureAwait(false).GetAwaiter().GetResult();

            if (_slab != null) Array.Clear(_slab);
            _slab = bytes;
            _slabStart = slabStart;
            _slabLen = bytes.Length;
        }

        var srcOffset = (int)(_position - _slabStart);
        var copy = Math.Min(want, _slabLen - srcOffset);
        if (copy <= 0) return 0;

        System.Runtime.InteropServices.Marshal.Copy(_slab!, srcOffset, buf, copy);
        _position += copy;
        return copy;
    }

    public override bool Seek(ulong offset)
    {
        if ((long)offset > _totalLength) return false;
        _position = (long)offset;
        return true;
    }

    public override void Close()
    {
        if (_slab != null) Array.Clear(_slab);
        _slab = null;
        _position = 0;
    }
}
