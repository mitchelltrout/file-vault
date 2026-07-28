using FileVault.Service.Crypto;
using FileVault.Service.VirtualFileSystem;
using FluentAssertions;

namespace FileVault.Service.Tests.VirtualFileSystem;

public class VfsIndexTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesTree()
    {
        var tree = new VfsTree();
        tree.MkDir("/Photos");
        tree.UpsertFile("/Photos/sunset.jpg", dataOffset: 1024, plaintextLength: 4096, encryptedLength: 4124);

        var bytes = VfsIndex.Serialize(tree);
        var restored = VfsIndex.Deserialize(bytes);

        var file = restored.Find("/Photos/sunset.jpg");
        file.Should().NotBeNull();
        file!.DataOffset.Should().Be(1024);
    }

    [Fact]
    public void EncryptAndDecrypt_RoundTrips()
    {
        using var key = new VaultKey(new byte[32]);
        var tree = new VfsTree();
        tree.MkDir("/Videos");
        tree.UpsertFile("/Videos/clip.mp4", 2048, 1_000_000, 1_000_028);

        var encrypted = VfsIndex.Encrypt(key, tree);
        var restored = VfsIndex.Decrypt(key, encrypted);

        restored.Find("/Videos/clip.mp4").Should().NotBeNull();
    }
}
