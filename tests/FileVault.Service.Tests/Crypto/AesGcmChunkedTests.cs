using FileVault.Service.Crypto;

namespace FileVault.Service.Tests.Crypto;

public class AesGcmChunkedTests
{
    [Fact]
    public void EncryptChunk_round_trips_with_matching_aad()
    {
        var key = new VaultKey(new byte[32]);
        var fileId = new byte[16];
        Random.Shared.NextBytes(fileId);
        var plaintext = new byte[1024];
        Random.Shared.NextBytes(plaintext);

        var enc = AesGcmChunked.EncryptChunk(key, plaintext, fileId, chunkIndex: 5);
        var dec = AesGcmChunked.DecryptChunk(key, enc, fileId, chunkIndex: 5);

        Assert.Equal(plaintext, dec);
    }

    [Fact]
    public void DecryptChunk_with_wrong_chunk_index_throws()
    {
        var key = new VaultKey(new byte[32]);
        var fileId = new byte[16];
        var plaintext = new byte[100];

        var enc = AesGcmChunked.EncryptChunk(key, plaintext, fileId, chunkIndex: 0);
        Assert.ThrowsAny<System.Security.Cryptography.AuthenticationTagMismatchException>(
            () => AesGcmChunked.DecryptChunk(key, enc, fileId, chunkIndex: 1));
    }

    [Fact]
    public void DecryptChunk_with_wrong_file_id_throws()
    {
        var key = new VaultKey(new byte[32]);
        var fileIdA = new byte[16];
        var fileIdB = new byte[16];
        fileIdB[0] = 0xFF;
        var plaintext = new byte[100];

        var enc = AesGcmChunked.EncryptChunk(key, plaintext, fileIdA, chunkIndex: 0);
        Assert.ThrowsAny<System.Security.Cryptography.AuthenticationTagMismatchException>(
            () => AesGcmChunked.DecryptChunk(key, enc, fileIdB, chunkIndex: 0));
    }

    [Fact]
    public void EncryptChunk_layout_is_nonce_then_ciphertext_then_tag()
    {
        var key = new VaultKey(new byte[32]);
        var plaintext = new byte[100];
        var enc = AesGcmChunked.EncryptChunk(key, plaintext, new byte[16], chunkIndex: 0);
        Assert.Equal(12 + 100 + 16, enc.Length);
    }
}
