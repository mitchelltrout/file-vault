using FileVault.Shared.Ipc.Messages;

namespace FileVault.UI.Ipc;

public interface IServiceClient
{
    Task CreateVaultAsync(string filePath, string displayName, string password,
        byte[]? coverImageBytes = null, CancellationToken ct = default);
    Task<string> UpdateVaultSettingsAsync(string vaultPath, byte[]? coverImageBytes, CancellationToken ct = default);
    Task<UnlockVaultResponse> UnlockVaultAsync(string filePath, string password, CancellationToken ct = default);
    Task LockVaultAsync(string filePath, CancellationToken ct = default);
    Task ChangePasswordAsync(string filePath, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<ListFolderResponse> ListFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default);
    Task ImportFilesAsync(string vaultPath, string targetFolder, IEnumerable<string> sourcePaths,
        string collisionBehavior, CancellationToken ct = default);
    Task ExportAsync(string vaultPath, string vaultNodePath, string destDir, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string vaultNodePath, CancellationToken ct = default);
    Task<byte[]> ReadFileAsync(string vaultPath, string vaultNodePath, long maxBytes = 52_428_800, CancellationToken ct = default);
    Task RenameAsync(string vaultPath, string vaultNodePath, string newName, CancellationToken ct = default);
    Task MoveAsync(string vaultPath, string sourcePath, string destFolder, CancellationToken ct = default);
    Task CreateFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default);
    Task<byte[]> ReadFileRangeAsync(string vaultPath, string vaultNodePath, long offset, int length, CancellationToken ct = default);
    Task SetRotationAsync(string vaultPath, string vaultNodePath, int rotationDegrees, CancellationToken ct = default);
}
