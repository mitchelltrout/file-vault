using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultOperations;

namespace FileVault.Service.FileOperations;

public static class ReadFileRangeOperation
{
    public const int MaxRangeBytes = 2 * 1024 * 1024; // 2 MB hard cap

    public static byte[] Read(VaultSession session, string vaultPath, long offset, int length)
    {
        using var _ = session.Lock.ReadLock();

        var node = session.Tree.Find(vaultPath)
            ?? throw new FileNotFoundException($"Not found: {vaultPath}");
        if (node.IsDirectory) throw new InvalidOperationException("Path is a directory.");

        // Clamp.
        if (length <= 0) return [];
        if (length > MaxRangeBytes) length = MaxRangeBytes;
        if (offset < 0 || offset >= node.PlaintextLength) return [];
        var clampedLength = (int)Math.Min(length, node.PlaintextLength - offset);

        // Legacy fallback: decrypt the entire file then slice.
        if (node.Chunks.Count == 0)
        {
            var whole = VaultContainerIo.ReadFileChunk(session.Stream, session.Key, node.DataOffset, node.PlaintextLength);
            var result = new byte[clampedLength];
            Array.Copy(whole, offset, result, 0, clampedLength);
            return result;
        }

        // Walk chunks, decrypt only those that overlap [offset, offset + clampedLength).
        var output = new byte[clampedLength];
        int written = 0;
        long chunkLogicalStart = 0;

        for (int i = 0; i < node.Chunks.Count && written < clampedLength; i++)
        {
            var chunk = node.Chunks[i];
            long chunkLogicalEnd = chunkLogicalStart + chunk.PlaintextLength;
            long rangeStart = offset + written;

            if (chunkLogicalEnd > rangeStart && chunkLogicalStart < offset + clampedLength)
            {
                var blob = VaultContainerIo.ReadChunkAt(session.Stream, chunk.ContainerOffset, chunk.CiphertextLength);
                var plain = AesGcmChunked.DecryptChunk(session.Key, blob, node.FileId, i);

                int srcStart = (int)Math.Max(0, rangeStart - chunkLogicalStart);
                int srcCount = Math.Min(plain.Length - srcStart, clampedLength - written);
                Array.Copy(plain, srcStart, output, written, srcCount);
                written += srcCount;
            }

            chunkLogicalStart = chunkLogicalEnd;
        }

        if (written != clampedLength)
        {
            // Defensive: shouldn't happen if the index is consistent.
            var trimmed = new byte[written];
            Array.Copy(output, trimmed, written);
            return trimmed;
        }
        return output;
    }
}
