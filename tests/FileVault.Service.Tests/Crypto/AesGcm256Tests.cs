using System.Security.Cryptography;
using FileVault.Service.Crypto;
using FluentAssertions;

namespace FileVault.Service.Tests.Crypto;

public class AesGcm256Tests
{
    [Fact]
    public void RoundTrip_ProducesOriginalPlaintext()
    {
        using var key = new VaultKey(new byte[32]);
        var plaintext = "Hello, FileVault!"u8.ToArray();

        var encrypted = AesGcm256.Encrypt(key, plaintext);
        var decrypted = AesGcm256.Decrypt(key, encrypted);

        decrypted.Should().Equal(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentNonceEachCall()
    {
        using var key = new VaultKey(new byte[32]);
        var plaintext = new byte[32];

        var enc1 = AesGcm256.Encrypt(key, plaintext);
        var enc2 = AesGcm256.Encrypt(key, plaintext);

        // First 12 bytes are the nonce — should differ
        enc1[..12].Should().NotEqual(enc2[..12]);
    }

    [Fact]
    public void Decrypt_ThrowsOnTamperedCiphertext()
    {
        using var key = new VaultKey(new byte[32]);
        var plaintext = new byte[32];
        var encrypted = AesGcm256.Encrypt(key, plaintext);
        encrypted[20]++; // tamper

        var act = () => AesGcm256.Decrypt(key, encrypted);
        act.Should().Throw<CryptographicException>();
    }
}
