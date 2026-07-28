using MessagePack;

namespace FileVault.Service.VirtualFileSystem;

[MessagePackObject]
public class ChunkRef
{
    [Key(0)] public long ContainerOffset { get; set; }
    [Key(1)] public int CiphertextLength { get; set; }
    [Key(2)] public int PlaintextLength { get; set; }
}
