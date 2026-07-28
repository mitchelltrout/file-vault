using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultFormat;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.Tests.VaultOperations;

public class DisguiseRoundTripTests
{
    [Fact]
    public void Vault_with_cover_prefix_unlocks_via_VaultStream()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            using var key = new VaultKey(new byte[32]); // test key
            var tree = new VfsTree();
            var header = new HeaderBlock("test", DateTimeOffset.UtcNow, 0, 0);
            var salt = new byte[32];

            // Build the undisguised vault payload in memory.
            var payload = new MemoryStream();
            VaultContainerIo.WriteNewVault(payload, key, salt, header, tree);

            // Write disguised file: 100 cover bytes + payload + trailer.
            var cover = new byte[100];
            for (int i = 0; i < 100; i++) cover[i] = (byte)i;
            VaultPrefix.WriteDisguisedFile(tmp, cover, payload);

            // Reopen, detect, wrap, and read header back.
            using var raw = new FileStream(tmp, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var baseOffset = VaultPrefix.DetectBaseOffset(raw);
            Assert.Equal(100, baseOffset);

            using var vs = new VaultStream(raw, baseOffset, leaveOpen: true);
            var roundTripped = VaultContainerIo.ReadHeaderBlock(vs, key);
            Assert.Equal("test", roundTripped.DisplayName);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
