using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VaultFormat;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.VaultOperations;

public class VaultManager : IDisposable
{
    private readonly Dictionary<string, VaultSession> _sessions = new();
    private readonly Lock _lock = new();

    public async Task CreateVaultAsync(string path, string displayName, string password,
        Argon2Params? argon2Params = null,
        byte[]? coverImageBytes = null)
    {
        var salt = KeyDerivation.GenerateSalt();
        using var key = KeyDerivation.Derive(password, salt, argon2Params);
        var block = new HeaderBlock(displayName, DateTimeOffset.UtcNow, indexOffset: 0, flags: 0);

        if (coverImageBytes is { Length: > 0 })
        {
            var hash = System.Security.Cryptography.SHA256.HashData(coverImageBytes);
            block.CoverImageBytes = coverImageBytes;
            block.CoverImageHash = hash;

            var payload = new MemoryStream();
            VaultContainerIo.WriteNewVault(payload, key, salt, block, new VfsTree());
            VaultPrefix.WriteDisguisedFile(path, coverImageBytes, payload);
        }
        else
        {
            // Build in memory for consistency, then write raw bytes.
            var payload = new MemoryStream();
            VaultContainerIo.WriteNewVault(payload, key, salt, block, new VfsTree());
            VaultPrefix.WriteDisguisedFile(path, null, payload);
        }
        await Task.CompletedTask;
    }

    public async Task<VaultSession> UnlockAsync(string path, string password,
        Argon2Params? argon2Params = null)
    {
        var rawStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        long baseOffset;
        try { baseOffset = VaultPrefix.DetectBaseOffset(rawStream); }
        catch { await rawStream.DisposeAsync(); throw; }

        var stream = new VaultStream(rawStream, baseOffset, leaveOpen: false);
        try
        {
            var plaintext = ContainerHeader.ReadPlaintext(stream);
            var key = KeyDerivation.Derive(password, plaintext.Salt, argon2Params);
            // ReadHeaderBlock will throw CryptographicException on wrong password
            var header = VaultContainerIo.ReadHeaderBlock(stream, key);
            var tree = VaultContainerIo.ReadIndex(stream, key, header.IndexOffset);
            var session = new VaultSession(path, header.DisplayName, key, tree, stream);

            lock (_lock) _sessions[path] = session;
            return session;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public void Lock(string path)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(path, out var session))
            {
                _sessions.Remove(path);
                session.Dispose();
            }
        }
    }

    public bool TryGetSession(string path, out VaultSession? session)
    {
        lock (_lock) return _sessions.TryGetValue(path, out session);
    }

    public VaultSession GetSession(string path)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(path, out var session))
                throw new InvalidOperationException($"No open session for vault: {path}");
            return session;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var session in _sessions.Values)
                session.Dispose();
            _sessions.Clear();
        }
    }

    public async Task ChangePasswordAsync(string path, string currentPassword, string newPassword,
        Argon2Params? argon2Params = null)
    {
        // Close any existing session first so the file is not locked
        Lock(path);

        // Verify current password by attempting unlock (throws on wrong password)
        // This also locks the file stream exclusively
        var session = await UnlockAsync(path, currentPassword, argon2Params);
        Lock(path); // dispose the session, releasing the FileStream

        var newSalt = KeyDerivation.GenerateSalt();
        using var newKey = KeyDerivation.Derive(newPassword, newSalt, argon2Params);

        // Read all file data from old container (may be disguised)
        var oldRaw = new FileStream(path, FileMode.Open, FileAccess.Read);
        Stream oldStream;
        try
        {
            var oldBaseOffset = VaultPrefix.DetectBaseOffset(oldRaw);
            oldStream = new VaultStream(oldRaw, oldBaseOffset, leaveOpen: false);
        }
        catch
        {
            oldRaw.Dispose();
            throw;
        }

        byte[]? coverImageBytes;
        string tempPath;
        try
        {
            var oldPlaintext = ContainerHeader.ReadPlaintext(oldStream);
            using var oldKey = KeyDerivation.Derive(currentPassword, oldPlaintext.Salt, argon2Params);
            var oldHeader = VaultContainerIo.ReadHeaderBlock(oldStream, oldKey);
            var oldTree = VaultContainerIo.ReadIndex(oldStream, oldKey, oldHeader.IndexOffset);

            // Preserve cover image across password change.
            coverImageBytes = oldHeader.CoverImageBytes is { Length: > 0 } ? oldHeader.CoverImageBytes : null;

            // Write new container with re-encrypted data
            tempPath = path + ".tmp";
            await using (var newStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite))
            {
                var newHeader = new HeaderBlock(oldHeader.DisplayName,
                    DateTimeOffset.FromUnixTimeSeconds(oldHeader.CreatedAtUtc), 0, 0)
                {
                    CoverImageBytes = oldHeader.CoverImageBytes,
                    CoverImageHash = oldHeader.CoverImageHash,
                };
                VaultContainerIo.WriteNewVault(newStream, newKey, newSalt, newHeader, new VfsTree());

                var newTree = new VfsTree();
                ReEncryptTree(oldStream, oldKey, newStream, newKey, oldTree.Root, "/", newTree);
                VaultContainerIo.RewriteIndex(newStream, newKey, newTree);
            }
        }
        finally
        {
            oldStream.Dispose();
        }

        if (coverImageBytes is { Length: > 0 })
        {
            // Re-wrap the new raw vault file in a disguise prefix to preserve cover image.
            var payload = new MemoryStream(File.ReadAllBytes(tempPath));
            VaultPrefix.WriteDisguisedFile(path, coverImageBytes, payload);
            File.Delete(tempPath);
        }
        else
        {
            File.Move(tempPath, path, overwrite: true);
        }
    }

    private static void ReEncryptTree(Stream oldStream, VaultKey oldKey,
        Stream newStream, VaultKey newKey,
        VfsNode node, string currentPath, VfsTree newTree)
    {
        foreach (var child in node.Children)
        {
            var childPath = currentPath.TrimEnd('/') + "/" + child.Name;
            if (child.IsDirectory)
            {
                newTree.MkDir(childPath);
                ReEncryptTree(oldStream, oldKey, newStream, newKey, child, childPath, newTree);
            }
            else if (child.Chunks.Count > 0)
            {
                // Chunked format: re-encrypt each chunk with new key and new file ID.
                var fileId = new byte[16];
                System.Security.Cryptography.RandomNumberGenerator.Fill(fileId);
                var newChunks = new List<VirtualFileSystem.ChunkRef>();
                for (int i = 0; i < child.Chunks.Count; i++)
                {
                    var chunk = child.Chunks[i];
                    var blob = VaultContainerIo.ReadChunkAt(oldStream, chunk.ContainerOffset, chunk.CiphertextLength);
                    var plain = Crypto.AesGcmChunked.DecryptChunk(oldKey, blob, child.FileId, i);
                    var enc = Crypto.AesGcmChunked.EncryptChunk(newKey, plain, fileId, i);
                    var off = VaultContainerIo.AppendChunkAt(newStream, enc);
                    newChunks.Add(new VirtualFileSystem.ChunkRef
                    {
                        ContainerOffset = off,
                        CiphertextLength = chunk.PlaintextLength,
                        PlaintextLength = chunk.PlaintextLength,
                    });
                }
                newTree.UpsertFileChunked(childPath, fileId, newChunks, child.PlaintextLength);
            }
            else
            {
                // Legacy format.
                var data = VaultContainerIo.ReadFileChunk(oldStream, oldKey,
                    child.DataOffset, child.PlaintextLength);
                var newOffset = VaultContainerIo.AppendFileChunk(newStream, newKey, data);
                newTree.UpsertFile(childPath, newOffset, child.PlaintextLength, child.EncryptedLength);
            }
        }
    }
}
