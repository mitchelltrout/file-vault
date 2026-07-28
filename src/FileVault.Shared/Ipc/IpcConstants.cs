// src/FileVault.Shared/Ipc/IpcConstants.cs
namespace FileVault.Shared.Ipc;

public static class IpcConstants
{
    public const string PipeName = "FileVaultService";
}

public enum MessageType : byte
{
    // Vault operations
    CreateVaultRequest = 1,
    CreateVaultResponse = 2,
    UnlockVaultRequest = 3,
    UnlockVaultResponse = 4,
    LockVaultRequest = 5,
    LockVaultResponse = 6,
    ChangePasswordRequest = 7,
    ChangePasswordResponse = 8,
    // File operations
    ListFolderRequest = 10,
    ListFolderResponse = 11,
    ImportFilesRequest = 12,
    ImportFilesResponse = 13,
    ExportRequest = 14,
    ExportResponse = 15,
    DeleteRequest = 16,
    DeleteResponse = 17,
    ReadFileRequest = 18,
    ReadFileResponse = 19,
    // Push notifications
    ProgressUpdate = 20,
    // More file operations
    RenameRequest = 21,
    RenameResponse = 22,
    MoveRequest = 23,
    MoveResponse = 24,
    CreateFolderRequest = 25,
    CreateFolderResponse = 26,
    UpdateVaultSettingsRequest = 27,
    UpdateVaultSettingsResponse = 28,
    ReadFileRangeRequest = 29,
    ReadFileRangeResponse = 30,
    SetRotationRequest = 31,
    SetRotationResponse = 32,
    // Errors
    ErrorResponse = 99,
}
