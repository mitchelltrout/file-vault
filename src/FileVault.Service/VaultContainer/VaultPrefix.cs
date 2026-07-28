using System.Buffers.Binary;

namespace FileVault.Service.VaultContainer;

/// <summary>
/// Disguise prefix detection and writing.
///
/// File layout when disguised:
///   [ cover JPEG bytes ][ FVLT magic + vault payload ][ FVDT trailer (16 bytes) ]
///
/// FVDT trailer:
///   [ "FVDT" : 4 ][ base_offset : 8 (LE int64) ][ "FVDT" : 4 ]
///
/// Detection is O(1): read trailer, validate magics, validate base_offset points at FVLT.
/// </summary>
public static class VaultPrefix
{
    private const int TrailerSize = 16;
    private static readonly byte[] FvltMagic = [0x46, 0x56, 0x4C, 0x54]; // "FVLT"
    private static readonly byte[] FvdtMagic = [0x46, 0x56, 0x44, 0x54]; // "FVDT"

    public static long DetectBaseOffset(Stream stream)
    {
        if (stream.Length < 4) throw new InvalidDataException("File too small to be a vault.");

        // Fast path: undisguised vault starts with FVLT.
        stream.Position = 0;
        var magic = new byte[4];
        stream.ReadExactly(magic);
        if (magic.AsSpan().SequenceEqual(FvltMagic)) return 0;

        // Disguised: parse trailer at end of file.
        if (stream.Length < TrailerSize) throw new InvalidDataException("File too small for disguise trailer.");
        stream.Position = stream.Length - TrailerSize;
        var trailer = new byte[TrailerSize];
        stream.ReadExactly(trailer);

        if (!trailer.AsSpan(0, 4).SequenceEqual(FvdtMagic) ||
            !trailer.AsSpan(12, 4).SequenceEqual(FvdtMagic))
            throw new InvalidDataException("Vault disguise trailer missing or corrupted.");

        var baseOffset = BinaryPrimitives.ReadInt64LittleEndian(trailer.AsSpan(4, 8));
        if (baseOffset < 0 || baseOffset + 4 > stream.Length - TrailerSize)
            throw new InvalidDataException("Vault disguise trailer offset out of range.");

        // Validate that base_offset points at FVLT.
        stream.Position = baseOffset;
        stream.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual(FvltMagic))
            throw new InvalidDataException("Vault disguise trailer offset does not point at vault payload.");

        return baseOffset;
    }

    /// <summary>
    /// Writes a disguised vault file at <paramref name="targetPath"/>:
    ///   [ coverImageBytes ][ vaultPayload ][ FVDT trailer ]
    /// If <paramref name="coverImageBytes"/> is null or empty, writes the payload only (no trailer).
    /// Atomically swaps over any existing file at <paramref name="targetPath"/>.
    /// </summary>
    public static void WriteDisguisedFile(string targetPath, byte[]? coverImageBytes, Stream vaultPayload)
    {
        var tmpPath = targetPath + ".tmp";
        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (coverImageBytes is { Length: > 0 })
                fs.Write(coverImageBytes);

            vaultPayload.Position = 0;
            vaultPayload.CopyTo(fs);

            if (coverImageBytes is { Length: > 0 })
                WriteTrailer(fs, baseOffset: coverImageBytes.Length);

            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(targetPath))
            File.Replace(tmpPath, targetPath, destinationBackupFileName: null);
        else
            File.Move(tmpPath, targetPath);
    }

    private static void WriteTrailer(Stream stream, long baseOffset)
    {
        stream.Write(FvdtMagic);
        Span<byte> off = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(off, baseOffset);
        stream.Write(off);
        stream.Write(FvdtMagic);
    }
}
