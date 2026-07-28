using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.FileOperations;

public enum CollisionBehavior { Replace, KeepBoth, Skip }

public static class ImportOperation
{
    public static async Task ImportFileAsync(VaultSession session, string targetVaultFolder,
        string sourceFilePath, CollisionBehavior collision, CancellationToken ct,
        IProgress<(string fileName, int filesRemaining)>? progress = null)
    {
        using var _ = session.Lock.WriteLock();

        var fileName = Path.GetFileName(sourceFilePath);
        var vaultPath = targetVaultFolder.TrimEnd('/') + "/" + fileName;
        vaultPath = ResolveCollision(session, vaultPath, collision);
        if (vaultPath is null) return; // Skip

        const int ChunkSize = 1024 * 1024; // 1 MB
        var fileId = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(fileId);

        var chunks = new List<ChunkRef>();
        long totalPlaintext = 0;

        await using (var src = File.OpenRead(sourceFilePath))
        {
            var buffer = new byte[ChunkSize];
            int chunkIndex = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var n = await src.ReadAsync(buffer.AsMemory(0, ChunkSize), ct);
                if (n == 0) break;

                var plain = n == ChunkSize ? buffer : buffer.AsSpan(0, n).ToArray();
                var enc = AesGcmChunked.EncryptChunk(session.Key, plain, fileId, chunkIndex);
                var off = VaultContainerIo.AppendChunkAt(session.Stream, enc);

                chunks.Add(new ChunkRef
                {
                    ContainerOffset = off,
                    CiphertextLength = n,
                    PlaintextLength = n,
                });
                totalPlaintext += n;
                chunkIndex++;

                if (n < ChunkSize) break;
            }
        }

        session.Tree.UpsertFileChunked(vaultPath, fileId, chunks, totalPlaintext);
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);

        progress?.Report((fileName, 0));
    }

    public static async Task ImportDirectoryAsync(VaultSession session, string targetVaultFolder,
        string sourceDir, CollisionBehavior collision, CancellationToken ct,
        IProgress<(string fileName, int filesRemaining)>? progress = null)
    {
        var dirName = Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar));
        var vaultBase = targetVaultFolder.TrimEnd('/') + "/" + dirName;
        session.Tree.MkDir(vaultBase);

        var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        for (int i = 0; i < allFiles.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(sourceDir, allFiles[i]);
            var relDir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
            var vaultDir = relDir.Length > 0
                ? vaultBase + "/" + relDir
                : vaultBase;
            session.Tree.MkDir(vaultDir);
            await ImportFileAsync(session, vaultDir, allFiles[i], collision, ct);
        }
        VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
    }

    private static string? ResolveCollision(VaultSession session, string vaultPath, CollisionBehavior collision)
    {
        if (session.Tree.Find(vaultPath) is null) return vaultPath;
        return collision switch
        {
            CollisionBehavior.Replace => vaultPath,
            CollisionBehavior.Skip => null,
            CollisionBehavior.KeepBoth => FindAvailableName(session, vaultPath),
            _ => vaultPath
        };
    }

    private static string FindAvailableName(VaultSession session, string vaultPath)
    {
        var dir = vaultPath[..vaultPath.LastIndexOf('/')];
        var name = Path.GetFileNameWithoutExtension(vaultPath);
        var ext = Path.GetExtension(vaultPath);
        int i = 1;
        string candidate;
        do { candidate = $"{dir}/{name} ({i++}){ext}"; }
        while (session.Tree.Find(candidate) is not null);
        return candidate;
    }
}
