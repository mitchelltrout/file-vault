using System.Buffers.Binary;

namespace FileVault.Service.VaultFormat;

public record PlaintextHeader(byte[] Salt, int EncryptedHeaderLength);

public static class ContainerHeader
{
    public static void WritePlaintext(Stream stream, byte[] salt, int encryptedHeaderLength)
    {
        stream.Position = 0;
        stream.Write(VaultConstants.Magic);
        var versionBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(versionBytes, VaultConstants.FormatVersion);
        stream.Write(versionBytes);
        stream.Write(salt);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, encryptedHeaderLength);
        stream.Write(lenBytes);
        // stream is now at VaultConstants.EncHeaderOffset (44), ready for the encrypted blob
    }

    public static PlaintextHeader ReadPlaintext(Stream stream)
    {
        stream.Position = 0;
        var magic = new byte[4];
        stream.ReadExactly(magic);
        if (!magic.SequenceEqual(VaultConstants.Magic))
            throw new InvalidDataException("Invalid vault file: bad magic bytes.");

        stream.Seek(4, SeekOrigin.Current); // skip version
        var salt = new byte[32];
        stream.ReadExactly(salt);
        var lenBytes = new byte[4];
        stream.ReadExactly(lenBytes);
        var encLen = BinaryPrimitives.ReadInt32LittleEndian(lenBytes);
        // stream is now at VaultConstants.EncHeaderOffset (44)
        return new PlaintextHeader(salt, encLen);
    }
}
