// src/FileVault.Shared/Ipc/Messages/PipeMessage.cs
using MessagePack;

namespace FileVault.Shared.Ipc.Messages;

[MessagePackObject]
public class PipeMessage
{
    [Key(0)] public MessageType Type { get; set; }
    [Key(1)] public Guid RequestId { get; set; }
    [Key(2)] public byte[] Payload { get; set; } = [];
}
