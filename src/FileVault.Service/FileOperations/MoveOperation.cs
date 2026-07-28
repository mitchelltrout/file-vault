using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class MoveOperation
{
    public static void Move(VaultSession session, string sourcePath, string destFolder)
    {
        if (!session.Tree.Move(sourcePath, destFolder))
            throw new FileNotFoundException($"Source not found: {sourcePath}");
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
    }
}
