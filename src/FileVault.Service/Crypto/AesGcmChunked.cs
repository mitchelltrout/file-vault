using System.Buffers.Binary;
using System.Security.Cryptography;

namespace FileVault.Service.Crypto;

public static class AesGcmChunked
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] EncryptChunk(VaultKey key, ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> fileId, int chunkIndex)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        Span<byte> aad = stackalloc byte[20];
        BuildAad(aad, fileId, chunkIndex);

        using var aes = new AesGcm(key.KeyBytes, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result.AsSpan(0));
        ciphertext.CopyTo(result.AsSpan(NonceSize));
        tag.CopyTo(result.AsSpan(NonceSize + ciphertext.Length));
        return result;
    }

    public static byte[] DecryptChunk(VaultKey key, ReadOnlySpan<byte> encrypted,
        ReadOnlySpan<byte> fileId, int chunkIndex)
    {
        if (encrypted.Length < NonceSize + TagSize)
            throw new ArgumentException("Encrypted chunk too short.", nameof(encrypted));

        var nonce = encrypted[..NonceSize];
        var tag = encrypted[^TagSize..];
        var ciphertext = encrypted[NonceSize..^TagSize];
        var plaintext = new byte[ciphertext.Length];
        Span<byte> aad = stackalloc byte[20];
        BuildAad(aad, fileId, chunkIndex);

        using var aes = new AesGcm(key.KeyBytes, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }

    private static void BuildAad(Span<byte> aad, ReadOnlySpan<byte> fileId, int chunkIndex)
    {
        if (fileId.Length != 16) throw new ArgumentException("FileId must be 16 bytes.", nameof(fileId));
        fileId.CopyTo(aad[..16]);
        BinaryPrimitives.WriteInt32LittleEndian(aad[16..], chunkIndex);
    }
}
