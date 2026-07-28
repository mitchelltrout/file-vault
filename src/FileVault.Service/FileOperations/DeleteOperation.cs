using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class DeleteOperation
{
    public static void Delete(VaultSession session, string vaultPath)
    {
        if (!session.Tree.Delete(vaultPath))
            throw new FileNotFoundException($"Vault path not found: {vaultPath}");
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
        // Note: deleted file chunks remain in the container (orphaned).
        // Compaction is a future feature.
    }
}
