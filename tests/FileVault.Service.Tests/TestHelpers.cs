using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultFormat;
using FileVault.Service.VaultOperations;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.Tests;

public static class TestHelpers
{
    public static VaultSession NewInMemoryVault()
    {
        var key = new VaultKey(new byte[32]);
        var ms = new MemoryStream();
        var header = new HeaderBlock("test", DateTimeOffset.UtcNow, 0, 0);
        var tree = new VfsTree();
        VaultContainerIo.WriteNewVault(ms, key, new byte[32], header, tree);
        var stream = new VaultStream(ms, baseOffset: 0, leaveOpen: true);
        return new VaultSession("memory://test", "test", key, tree, stream);
    }
}
