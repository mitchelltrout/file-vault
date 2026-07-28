using MessagePack;

namespace FileVault.Service.VirtualFileSystem;

[MessagePackObject]
public class VfsNode
{
    [Key(0)] public string Name { get; set; } = "";
    [Key(1)] public bool IsDirectory { get; set; }
    [Key(2)] public long CreatedAtUtc { get; set; }
    [Key(3)] public long ModifiedAtUtc { get; set; }
    // File-only fields
    [Key(4)] public long DataOffset { get; set; }
    [Key(5)] public long PlaintextLength { get; set; }
    [Key(6)] public long EncryptedLength { get; set; }
    // Directory-only field
    [Key(7)] public List<VfsNode> Children { get; set; } = [];
    // Chunked file fields (new format; empty for legacy nodes)
    [Key(8)] public byte[] FileId { get; set; } = [];
    [Key(9)] public List<ChunkRef> Chunks { get; set; } = [];
    // User-set rotation (0, 90, 180, 270). Defaults to 0 for old nodes.
    [Key(10)] public int RotationDegrees { get; set; }

    public static VfsNode NewFolder(string name) => new()
    {
        Name = name,
        IsDirectory = true,
        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ModifiedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };

    public static VfsNode NewFile(string name, long dataOffset, long plaintextLength, long encryptedLength) => new()
    {
        Name = name,
        IsDirectory = false,
        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ModifiedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        DataOffset = dataOffset,
        PlaintextLength = plaintextLength,
        EncryptedLength = encryptedLength
    };
}
