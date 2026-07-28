using System.Buffers.Binary;
using FileVault.Service.Crypto;
using FileVault.Service.VaultFormat;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.VaultContainer;

public static class VaultContainerIo
{
    public static void WriteNewVault(Stream stream, VaultKey key, byte[] salt,
        HeaderBlock block, VfsTree tree)
    {
        stream.SetLength(0);
        stream.Position = 0;

        // Step 1: Write plaintext header with placeholder length (0); update after.
        ContainerHeader.WritePlaintext(stream, salt, encryptedHeaderLength: 0);
        // stream is now at EncHeaderOffset (44)

        // Step 2: Encrypt header block with placeholder index offset
        block.IndexOffset = 0;
        var encHeader = HeaderBlock.Encrypt(key, block); // nonce embedded by AesGcm256

        // Step 3: Write real encrypted-header length (plaintext length = encHeader.Length - 28)
        stream.Position = VaultConstants.EncHeaderLenOffset;
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, encHeader.Length - 28);
        stream.Write(lenBytes);
        stream.Position = VaultConstants.EncHeaderOffset;
        stream.Write(encHeader);

        // Step 4: Write the initial (empty) index immediately after the header block
        var indexOffset = stream.Position;
        WriteIndexAt(stream, key, tree);

        // Step 5: Rewrite the header block with the correct index offset
        block.IndexOffset = indexOffset;
        var finalEncHeader = HeaderBlock.Encrypt(key, block);
        // Also update the length in the plaintext header (same size, but rewrite for safety)
        stream.Position = VaultConstants.EncHeaderLenOffset;
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, finalEncHeader.Length - 28);
        stream.Write(lenBytes);
        stream.Position = VaultConstants.EncHeaderOffset;
        stream.Write(finalEncHeader);
        stream.Flush();
    }

    public static HeaderBlock ReadHeaderBlock(Stream stream, VaultKey key)
    {
        var plaintext = ContainerHeader.ReadPlaintext(stream);
        // ReadPlaintext leaves stream at EncHeaderOffset (44)
        var encHeaderBytes = new byte[plaintext.EncryptedHeaderLength + 28]; // +28 = 12 nonce + 16 tag
        stream.ReadExactly(encHeaderBytes);
        return HeaderBlock.Decrypt(key, encHeaderBytes);
    }

    public static long AppendFileChunk(Stream stream, VaultKey key, byte[] plaintext)
    {
        stream.Seek(0, SeekOrigin.End);
        var offset = stream.Position;
        var encrypted = AesGcm256.Encrypt(key, plaintext);
        var lenBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(lenBytes, plaintext.Length);
        stream.Write(lenBytes);
        stream.Write(encrypted);
        stream.Flush();
        return offset;
    }

    public static byte[] ReadFileChunk(Stream stream, VaultKey key, long offset, long plaintextLength)
    {
        stream.Position = offset;
        stream.Seek(8, SeekOrigin.Current); // skip stored plaintext length
        var encryptedLength = plaintextLength + 28; // nonce + tag overhead
        var encrypted = new byte[encryptedLength];
        stream.ReadExactly(encrypted);
        return AesGcm256.Decrypt(key, encrypted);
    }

    public static void RewriteIndex(Stream stream, VaultKey key, VfsTree tree)
    {
        stream.Seek(0, SeekOrigin.End);
        var indexOffset = stream.Position;
        WriteIndexAt(stream, key, tree);

        // Update header block with new index offset
        stream.Position = 0;
        var header = ReadHeaderBlock(stream, key);
        header.IndexOffset = indexOffset;
        var encHeader = HeaderBlock.Encrypt(key, header);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, encHeader.Length - 28);
        stream.Position = VaultConstants.EncHeaderLenOffset;
        stream.Write(lenBytes);
        stream.Position = VaultConstants.EncHeaderOffset;
        stream.Write(encHeader);
        stream.Flush();
    }

    public static VfsTree ReadIndex(Stream stream, VaultKey key, long indexOffset)
    {
        stream.Position = indexOffset;
        var lenBytes = new byte[4];
        stream.ReadExactly(lenBytes);
        var encLen = BinaryPrimitives.ReadInt32LittleEndian(lenBytes);
        var encrypted = new byte[encLen + 28];
        stream.ReadExactly(encrypted);
        return VfsIndex.Decrypt(key, encrypted);
    }

    /// <summary>Appends an encrypted chunk and returns its container offset (start of the [nonce|ct|tag] blob).</summary>
    public static long AppendChunkAt(Stream stream, byte[] encryptedChunk)
    {
        stream.Seek(0, SeekOrigin.End);
        var offset = stream.Position;
        stream.Write(encryptedChunk);
        stream.Flush();
        return offset;
    }

    /// <summary>Reads an encrypted chunk back from the given container offset.</summary>
    public static byte[] ReadChunkAt(Stream stream, long containerOffset, int ciphertextLength)
    {
        stream.Position = containerOffset;
        var blob = new byte[ciphertextLength + 12 + 16];
        stream.ReadExactly(blob);
        return blob;
    }

    private static void WriteIndexAt(Stream stream, VaultKey key, VfsTree tree)
    {
        var encrypted = VfsIndex.Encrypt(key, tree);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, encrypted.Length - 28);
        stream.Write(lenBytes);
        stream.Write(encrypted);
    }
}
