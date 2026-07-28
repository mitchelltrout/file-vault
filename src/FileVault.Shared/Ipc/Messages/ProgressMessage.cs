// src/FileVault.Shared/Ipc/Messages/ProgressMessage.cs
using MessagePack;

namespace FileVault.Shared.Ipc.Messages;

[MessagePackObject] public class ProgressMessage
{
    [Key(0)] public Guid OperationId { get; set; }
    [Key(1)] public string CurrentFile { get; set; } = "";
    [Key(2)] public int FilesRemaining { get; set; }
}
