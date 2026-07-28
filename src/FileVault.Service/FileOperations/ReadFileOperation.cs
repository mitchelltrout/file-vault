using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class ReadFileOperation
{
    public static byte[] Read(VaultSession session, string vaultPath, long maxBytes)
    {
        using var _ = session.Lock.ReadLock();

        var node = session.Tree.Find(vaultPath)
            ?? throw new FileNotFoundException($"Vault path not found: {vaultPath}");
        if (node.IsDirectory)
            throw new InvalidOperationException($"Cannot read a directory: {vaultPath}");
        if (node.PlaintextLength > maxBytes)
            throw new InvalidOperationException(
                $"File exceeds max read size ({node.PlaintextLength} > {maxBytes} bytes).");

        if (node.Chunks.Count > 0)
        {
            // New chunked format.
            var output = new byte[node.PlaintextLength];
            long writePos = 0;
            for (int i = 0; i < node.Chunks.Count; i++)
            {
                var chunk = node.Chunks[i];
                var blob = VaultContainerIo.ReadChunkAt(session.Stream, chunk.ContainerOffset, chunk.CiphertextLength);
                var plain = AesGcmChunked.DecryptChunk(session.Key, blob, node.FileId, i);
                plain.CopyTo(output, writePos);
                writePos += plain.Length;
            }
            return output;
        }
        else
        {
            // Legacy single-blob format.
            return VaultContainerIo.ReadFileChunk(session.Stream, session.Key,
                node.DataOffset, node.PlaintextLength);
        }
    }
}
