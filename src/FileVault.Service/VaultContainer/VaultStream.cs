namespace FileVault.Service.VaultContainer;

/// <summary>
/// A Stream wrapper that exposes a sub-range of an inner stream starting at <c>BaseOffset</c>.
/// All Position / Seek / Length values are translated so callers can treat the stream as if
/// offset 0 were at <c>BaseOffset</c> in the underlying file.
/// </summary>
public sealed class VaultStream : Stream
{
    private readonly Stream _inner;
    private readonly long _baseOffset;
    private readonly bool _leaveOpen;

    public VaultStream(Stream inner, long baseOffset, bool leaveOpen = false)
    {
        if (!inner.CanSeek) throw new ArgumentException("Inner stream must be seekable.", nameof(inner));
        if (baseOffset < 0) throw new ArgumentOutOfRangeException(nameof(baseOffset));
        _inner = inner;
        _baseOffset = baseOffset;
        _leaveOpen = leaveOpen;
    }

    public long BaseOffset => _baseOffset;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => true;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length - _baseOffset;

    public override long Position
    {
        get => _inner.Position - _baseOffset;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _inner.Position = value + _baseOffset;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        _inner.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin   => _baseOffset + offset,
            SeekOrigin.Current => _inner.Position + offset,
            SeekOrigin.End     => _inner.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        if (target < _baseOffset) throw new IOException("Seek before start of vault region.");
        _inner.Position = target;
        return target - _baseOffset;
    }

    public override void SetLength(long value) => _inner.SetLength(value + _baseOffset);
    public override void Flush() => _inner.Flush();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen) _inner.Dispose();
        base.Dispose(disposing);
    }
}
