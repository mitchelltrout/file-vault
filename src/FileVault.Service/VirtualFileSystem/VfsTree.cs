namespace FileVault.Service.VirtualFileSystem;

public class VfsTree
{
    public VfsNode Root { get; private set; } = VfsNode.NewFolder("/");

    public VfsTree() { }
    public VfsTree(VfsNode root) { Root = root; }

    private static string[] SplitPath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private VfsNode? TraverseTo(string path, bool createMissing = false)
    {
        var parts = SplitPath(path);
        var current = Root;
        foreach (var part in parts)
        {
            var child = current.Children.FirstOrDefault(n => n.Name == part);
            if (child is null)
            {
                if (!createMissing) return null;
                child = VfsNode.NewFolder(part);
                current.Children.Add(child);
            }
            current = child;
        }
        return current;
    }

    public void MkDir(string path)
    {
        TraverseTo(path, createMissing: true);
    }

    public void UpsertFile(string path, long dataOffset, long plaintextLength, long encryptedLength)
    {
        var parts = SplitPath(path);
        var parentPath = string.Join("/", parts[..^1]);
        var parent = TraverseTo("/" + parentPath, createMissing: true)!;
        var name = parts[^1];
        var existing = parent.Children.FirstOrDefault(n => n.Name == name);
        if (existing is not null) parent.Children.Remove(existing);
        parent.Children.Add(VfsNode.NewFile(name, dataOffset, plaintextLength, encryptedLength));
    }

    public void UpsertFileChunked(string path, byte[] fileId, List<ChunkRef> chunks, long totalPlaintext)
    {
        var parts = SplitPath(path);
        var parentPath = string.Join("/", parts[..^1]);
        var parent = TraverseTo("/" + parentPath, createMissing: true)!;
        var name = parts[^1];
        var existing = parent.Children.FirstOrDefault(n => n.Name == name);
        if (existing is not null) parent.Children.Remove(existing);

        var node = new VfsNode
        {
            Name = name,
            IsDirectory = false,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ModifiedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            FileId = fileId,
            Chunks = chunks,
            PlaintextLength = totalPlaintext,
        };
        parent.Children.Add(node);
    }

    public VfsNode? Find(string path)
    {
        if (path == "/") return Root;
        return TraverseTo(path);
    }

    public IReadOnlyList<VfsNode> ListFolder(string path)
    {
        var node = Find(path);
        if (node is null || !node.IsDirectory) return [];
        return node.Children;
    }

    public bool Rename(string path, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Contains('/'))
            throw new ArgumentException("Invalid name", nameof(newName));
        var parts = SplitPath(path);
        if (parts.Length == 0) throw new InvalidOperationException("Cannot rename root.");
        var parentPath = "/" + string.Join("/", parts[..^1]);
        var parent = Find(parentPath);
        if (parent is null) return false;
        var node = parent.Children.FirstOrDefault(n => n.Name == parts[^1]);
        if (node is null) return false;
        if (parent.Children.Any(n => n.Name == newName && !ReferenceEquals(n, node)))
            throw new InvalidOperationException($"An item named '{newName}' already exists.");
        node.Name = newName;
        return true;
    }

    public bool Move(string sourcePath, string destFolder)
    {
        var srcParts = SplitPath(sourcePath);
        if (srcParts.Length == 0) throw new InvalidOperationException("Cannot move root.");
        var srcParentPath = "/" + string.Join("/", srcParts[..^1]);
        var srcParent = Find(srcParentPath);
        if (srcParent is null) return false;
        var node = srcParent.Children.FirstOrDefault(n => n.Name == srcParts[^1]);
        if (node is null) return false;

        var dest = TraverseTo(destFolder, createMissing: true);
        if (dest is null || !dest.IsDirectory) return false;
        // prevent moving folder into itself / descendant
        if (node.IsDirectory)
        {
            var normSrc = sourcePath.TrimEnd('/') + "/";
            var normDst = destFolder.TrimEnd('/') + "/";
            if (normDst.StartsWith(normSrc, StringComparison.Ordinal))
                throw new InvalidOperationException("Cannot move a folder into itself.");
        }
        if (dest.Children.Any(n => n.Name == node.Name))
            throw new InvalidOperationException($"An item named '{node.Name}' already exists in destination.");

        srcParent.Children.Remove(node);
        dest.Children.Add(node);
        return true;
    }

    public bool Delete(string path)
    {
        var parts = SplitPath(path);
        if (parts.Length == 0) return false;
        var parentPath = "/" + string.Join("/", parts[..^1]);
        var parent = Find(parentPath);
        if (parent is null) return false;
        var name = parts[^1];
        var child = parent.Children.FirstOrDefault(n => n.Name == name);
        if (child is null) return false;
        parent.Children.Remove(child);
        return true;
    }
}
