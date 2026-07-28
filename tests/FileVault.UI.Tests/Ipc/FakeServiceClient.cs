using FileVault.Shared.Ipc.Messages;
using FileVault.UI.Ipc;

namespace FileVault.UI.Tests.Ipc;

public class FakeServiceClient : IServiceClient
{
    public List<string> Calls { get; } = [];
    public Dictionary<string, UnlockVaultResponse> UnlockResponses { get; } = [];
    public Dictionary<string, ListFolderResponse> FolderResponses { get; } = [];
    public Dictionary<string, byte[]> FileBytes { get; } = [];
    public Exception? ThrowOn { get; set; }

    public Task CreateVaultAsync(string filePath, string displayName, string password,
        byte[]? coverImageBytes = null, CancellationToken ct = default)
    {
        Calls.Add($"Create:{filePath}:{displayName}");
        if (ThrowOn is not null) throw ThrowOn;
        return Task.CompletedTask;
    }

    public Task<string> UpdateVaultSettingsAsync(string vaultPath, byte[]? coverImageBytes, CancellationToken ct = default)
    {
        Calls.Add($"UpdateVaultSettings:{vaultPath}");
        if (ThrowOn is not null) throw ThrowOn;
        return Task.FromResult(vaultPath);
    }

    public Task<UnlockVaultResponse> UnlockVaultAsync(string filePath, string password, CancellationToken ct = default)
    {
        Calls.Add($"Unlock:{filePath}");
        if (ThrowOn is not null) throw ThrowOn;
        return Task.FromResult(UnlockResponses.TryGetValue(filePath, out var r)
            ? r : new UnlockVaultResponse { DisplayName = "Test Vault" });
    }

    public Task LockVaultAsync(string filePath, CancellationToken ct = default)
    {
        Calls.Add($"Lock:{filePath}");
        return Task.CompletedTask;
    }

    public Task ChangePasswordAsync(string filePath, string current, string newPw, CancellationToken ct = default)
    {
        Calls.Add($"ChangePassword:{filePath}");
        if (ThrowOn is not null) throw ThrowOn;
        return Task.CompletedTask;
    }

    public Task<ListFolderResponse> ListFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default)
    {
        Calls.Add($"ListFolder:{vaultPath}:{folderPath}");
        return Task.FromResult(FolderResponses.TryGetValue(folderPath, out var r)
            ? r : new ListFolderResponse());
    }

    public Task ImportFilesAsync(string vaultPath, string target, IEnumerable<string> sources,
        string collision, CancellationToken ct = default)
    {
        Calls.Add($"Import:{vaultPath}:{target}");
        return Task.CompletedTask;
    }

    public Task ExportAsync(string vaultPath, string nodePath, string destDir, CancellationToken ct = default)
    {
        Calls.Add($"Export:{vaultPath}:{nodePath}");
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string vaultPath, string nodePath, CancellationToken ct = default)
    {
        Calls.Add($"Delete:{vaultPath}:{nodePath}");
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadFileAsync(string vaultPath, string vaultNodePath,
        long maxBytes = 52_428_800, CancellationToken ct = default)
    {
        Calls.Add($"ReadFile:{vaultPath}:{vaultNodePath}");
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task RenameAsync(string vaultPath, string vaultNodePath, string newName, CancellationToken ct = default)
    {
        Calls.Add($"Rename:{vaultPath}:{vaultNodePath}:{newName}");
        return Task.CompletedTask;
    }

    public Task MoveAsync(string vaultPath, string sourcePath, string destFolder, CancellationToken ct = default)
    {
        Calls.Add($"Move:{vaultPath}:{sourcePath}:{destFolder}");
        return Task.CompletedTask;
    }

    public Task CreateFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default)
    {
        Calls.Add($"CreateFolder:{vaultPath}:{folderPath}");
        return Task.CompletedTask;
    }

    public Task SetRotationAsync(string vaultPath, string vaultNodePath, int rotationDegrees, CancellationToken ct = default)
    {
        Calls.Add($"SetRotation:{vaultPath}:{vaultNodePath}:{rotationDegrees}");
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadFileRangeAsync(string vaultPath, string vaultNodePath, long offset, int length, CancellationToken ct = default)
    {
        Calls.Add($"ReadFileRange({vaultNodePath}, {offset}, {length})");
        if (FileBytes.TryGetValue(vaultNodePath, out var data))
        {
            if (offset >= data.Length) return Task.FromResult(Array.Empty<byte>());
            var clamped = (int)Math.Min(length, data.Length - offset);
            return Task.FromResult(data.AsSpan((int)offset, clamped).ToArray());
        }
        return Task.FromResult(Array.Empty<byte>());
    }
}
