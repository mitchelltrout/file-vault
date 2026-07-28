using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultFormat;
using FileVault.Service.VirtualFileSystem;
using FluentAssertions;

namespace FileVault.Service.Tests.VaultContainer;

public class VaultContainerTests
{
    private static (VaultKey key, byte[] salt) MakeKey()
    {
        var salt = KeyDerivation.GenerateSalt();
        var key = KeyDerivation.Derive("testpass", salt, KeyDerivation.FastParams);
        return (key, salt);
    }

    [Fact]
    public void WriteNewVault_CanReadBackHeaderBlock()
    {
        var (key, salt) = MakeKey();
        var stream = new MemoryStream();
        var block = new HeaderBlock("Test Vault", DateTimeOffset.UtcNow, indexOffset: 0, flags: 0);

        VaultContainerIo.WriteNewVault(stream, key, salt, block, new VfsTree());
        stream.Position = 0;
        var readBack = VaultContainerIo.ReadHeaderBlock(stream, key);

        readBack.DisplayName.Should().Be("Test Vault");
    }

    [Fact]
    public void AppendAndReadChunk_RoundTrips()
    {
        var (key, salt) = MakeKey();
        var stream = new MemoryStream();
        VaultContainerIo.WriteNewVault(stream, key, salt,
            new HeaderBlock("T", DateTimeOffset.UtcNow, 0, 0), new VfsTree());

        var data = "Hello, Chunk!"u8.ToArray();
        var offset = VaultContainerIo.AppendFileChunk(stream, key, data);
        var readBack = VaultContainerIo.ReadFileChunk(stream, key, offset, data.Length);

        readBack.Should().Equal(data);
    }

    [Fact]
    public void RewriteIndex_UpdatesIndexOffset()
    {
        var (key, salt) = MakeKey();
        var stream = new MemoryStream();
        VaultContainerIo.WriteNewVault(stream, key, salt,
            new HeaderBlock("T", DateTimeOffset.UtcNow, 0, 0), new VfsTree());

        var tree = new VfsTree();
        tree.MkDir("/Photos");
        VaultContainerIo.RewriteIndex(stream, key, tree);

        stream.Position = 0;
        var header = VaultContainerIo.ReadHeaderBlock(stream, key);
        var restoredTree = VaultContainerIo.ReadIndex(stream, key, header.IndexOffset);
        restoredTree.Find("/Photos").Should().NotBeNull();
    }
}
