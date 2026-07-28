// src/FileVault.Shared/Ipc/Messages/FileMessages.cs
using MessagePack;

namespace FileVault.Shared.Ipc.Messages;

[MessagePackObject] public class ListFolderRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string FolderPath { get; set; } = "/";
}

[MessagePackObject] public class VfsNodeDto
{
    [Key(0)] public string Name { get; set; } = "";
    [Key(1)] public bool IsDirectory { get; set; }
    [Key(2)] public long PlaintextLength { get; set; }
    [Key(3)] public long ModifiedAtUtc { get; set; }
    [Key(4)] public int RotationDegrees { get; set; }
}

[MessagePackObject] public class ListFolderResponse
{
    [Key(0)] public List<VfsNodeDto> Nodes { get; set; } = [];
}

[MessagePackObject] public class ImportFilesRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string TargetVaultFolder { get; set; } = "/";
    [Key(2)] public List<string> SourcePaths { get; set; } = [];
    [Key(3)] public string CollisionBehavior { get; set; } = "KeepBoth";
}

[MessagePackObject] public class ExportRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
    [Key(2)] public string DestinationDirectory { get; set; } = "";
}

[MessagePackObject] public class DeleteRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
}

[MessagePackObject] public class FileOperationResponse
{
    [Key(0)] public bool Success { get; set; }
}

[MessagePackObject] public class ReadFileRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
    [Key(2)] public long MaxBytes { get; set; } = 52_428_800; // 50 MB guard
}

[MessagePackObject] public class ReadFileResponse
{
    [Key(0)] public byte[] Data { get; set; } = [];
}

[MessagePackObject] public class RenameRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
    [Key(2)] public string NewName { get; set; } = "";
}

[MessagePackObject] public class MoveRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string SourcePath { get; set; } = "";
    [Key(2)] public string DestinationFolder { get; set; } = "";
}

[MessagePackObject] public class CreateFolderRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string FolderPath { get; set; } = "";
}

[MessagePackObject] public class ReadFileRangeRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
    [Key(2)] public long Offset { get; set; }
    [Key(3)] public int Length { get; set; }
}

[MessagePackObject] public class ReadFileRangeResponse
{
    [Key(0)] public byte[] Bytes { get; set; } = [];
}

[MessagePackObject] public class SetRotationRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public string VaultNodePath { get; set; } = "";
    [Key(2)] public int RotationDegrees { get; set; }
}

[MessagePackObject] public class SetRotationResponse
{
    [Key(0)] public bool Success { get; set; }
}
