using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class RenameOperation
{
    public static void Rename(VaultSession session, string vaultPath, string newName)
    {
        if (!session.Tree.Rename(vaultPath, newName))
            throw new FileNotFoundException($"Vault path not found: {vaultPath}");
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
    }
}
