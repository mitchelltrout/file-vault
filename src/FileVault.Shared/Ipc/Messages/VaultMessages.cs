// src/FileVault.Shared/Ipc/Messages/VaultMessages.cs
using MessagePack;

namespace FileVault.Shared.Ipc.Messages;

[MessagePackObject] public class CreateVaultRequest
{
    [Key(0)] public string FilePath { get; set; } = "";
    [Key(1)] public string DisplayName { get; set; } = "";
    [Key(2)] public string Password { get; set; } = "";
    [Key(3)] public byte[] CoverImageBytes { get; set; } = [];
}

[MessagePackObject] public class UpdateVaultSettingsRequest
{
    [Key(0)] public string VaultPath { get; set; } = "";
    [Key(1)] public byte[] CoverImageBytes { get; set; } = []; // empty = remove disguise
}

[MessagePackObject] public class UpdateVaultSettingsResponse
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string NewVaultPath { get; set; } = "";
}

[MessagePackObject] public class UnlockVaultRequest
{
    [Key(0)] public string FilePath { get; set; } = "";
    [Key(1)] public string Password { get; set; } = "";
}

[MessagePackObject] public class UnlockVaultResponse
{
    [Key(0)] public string DisplayName { get; set; } = "";
}

[MessagePackObject] public class LockVaultRequest
{
    [Key(0)] public string FilePath { get; set; } = "";
}

[MessagePackObject] public class ChangePasswordRequest
{
    [Key(0)] public string FilePath { get; set; } = "";
    [Key(1)] public string CurrentPassword { get; set; } = "";
    [Key(2)] public string NewPassword { get; set; } = "";
}

[MessagePackObject] public class VaultOperationResponse
{
    [Key(0)] public bool Success { get; set; }
}

[MessagePackObject] public class ErrorResponse
{
    [Key(0)] public string Message { get; set; } = "";
}
