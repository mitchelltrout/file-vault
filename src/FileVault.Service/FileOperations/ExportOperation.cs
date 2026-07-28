using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.FileOperations;

public static class ExportOperation
{
    public static async Task ExportAsync(VaultSession session, string vaultPath,
        string destDir, CancellationToken ct,
        IProgress<(string fileName, int filesRemaining)>? progress = null)
    {
        var node = session.Tree.Find(vaultPath);
        if (node is null) throw new FileNotFoundException($"Vault path not found: {vaultPath}");

        if (!node.IsDirectory)
            await ExportFileNode(session, node, destDir, ct);
        else
            await ExportFolderNode(session, node, destDir, ct, progress);
    }

    private static async Task ExportFileNode(VaultSession session, VfsNode node,
        string destDir, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        byte[] data;
        if (node.Chunks.Count > 0)
        {
            data = new byte[node.PlaintextLength];
            long writePos = 0;
            for (int i = 0; i < node.Chunks.Count; i++)
            {
                var chunk = node.Chunks[i];
                var blob = VaultContainerIo.ReadChunkAt(session.Stream, chunk.ContainerOffset, chunk.CiphertextLength);
                var plain = AesGcmChunked.DecryptChunk(session.Key, blob, node.FileId, i);
                plain.CopyTo(data, writePos);
                writePos += plain.Length;
            }
        }
        else
        {
            data = VaultContainerIo.ReadFileChunk(session.Stream, session.Key,
                node.DataOffset, node.PlaintextLength);
        }
        var dest = Path.Combine(destDir, node.Name);
        await File.WriteAllBytesAsync(dest, data, ct);
    }

    private static async Task ExportFolderNode(VaultSession session, VfsNode node,
        string destDir, CancellationToken ct,
        IProgress<(string fileName, int filesRemaining)>? progress)
    {
        var dir = Path.Combine(destDir, node.Name);
        Directory.CreateDirectory(dir);
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
                await ExportFolderNode(session, child, dir, ct, progress);
            else
                await ExportFileNode(session, child, dir, ct);
        }
    }
}
