using System.Security.Cryptography;

namespace FileVault.Service.Crypto;

public static class AesGcm256
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] Encrypt(VaultKey key, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key.KeyBytes, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NonceSize);
        tag.CopyTo(result, NonceSize + ciphertext.Length);
        return result;
    }

    public static byte[] Decrypt(VaultKey key, ReadOnlySpan<byte> encrypted)
    {
        if (encrypted.Length < NonceSize + TagSize)
            throw new ArgumentException("Encrypted data too short.", nameof(encrypted));

        var nonce = encrypted[..NonceSize];
        var tag = encrypted[^TagSize..];
        var ciphertext = encrypted[NonceSize..^TagSize];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key.KeyBytes, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
