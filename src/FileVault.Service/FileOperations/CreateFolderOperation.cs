using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class CreateFolderOperation
{
    public static void CreateFolder(VaultSession session, string folderPath)
    {
        session.Tree.MkDir(folderPath);
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
    }
}
