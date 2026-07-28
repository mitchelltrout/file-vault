using FileVault.Service.Crypto;
using FileVault.Service.VaultContainer;
using FileVault.Service.VirtualFileSystem;

namespace FileVault.Service.VaultOperations;

public sealed class VaultSession : IDisposable
{
    public string VaultPath { get; private set; }
    public string DisplayName { get; }
    internal VaultKey Key { get; }
    internal VfsTree Tree { get; }
    internal Stream Stream { get; private set; }
    public VaultLock Lock { get; } = new();
    private bool _disposed;

    internal VaultSession(string vaultPath, string displayName, VaultKey key, VfsTree tree, Stream stream)
    {
        VaultPath = vaultPath;
        DisplayName = displayName;
        Key = key;
        Tree = tree;
        Stream = stream;
    }

    /// <summary>
    /// Closes the current backing stream without disposing the session. Use in conjunction
    /// with <see cref="ReopenAt(string)"/> when the underlying file must be replaced on disk.
    /// </summary>
    internal void CloseStream()
    {
        Stream.Dispose();
    }

    /// <summary>
    /// Reopens the vault's underlying file at <paramref name="newPath"/>, detecting disguise
    /// prefix and wrapping in a <see cref="VaultStream"/>. Updates <see cref="VaultPath"/>.
    /// </summary>
    internal void ReopenAt(string newPath)
    {
        var raw = new FileStream(newPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var baseOffset = VaultPrefix.DetectBaseOffset(raw);
            Stream = new VaultStream(raw, baseOffset, leaveOpen: false);
        }
        catch
        {
            raw.Dispose();
            throw;
        }
        VaultPath = newPath;
    }

    internal void ReplaceStream(Stream s)
    {
        Stream = s;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Key.Dispose();
        Stream.Dispose();
    }
}
