// src/FileVault.Service/Ipc/Handlers/VaultHandlers.cs
using FileVault.Service.VaultOperations;
using FileVault.Shared.Ipc.Messages;

namespace FileVault.Service.Ipc.Handlers;

public class VaultHandlers(VaultManager manager)
{
    public async Task<VaultOperationResponse> CreateAsync(CreateVaultRequest req, CancellationToken ct)
    {
        await manager.CreateVaultAsync(req.FilePath, req.DisplayName, req.Password,
            coverImageBytes: req.CoverImageBytes);
        return new VaultOperationResponse { Success = true };
    }

    public UpdateVaultSettingsResponse UpdateSettings(UpdateVaultSettingsRequest req)
    {
        var session = manager.GetSession(req.VaultPath);
        var newPath = UpdateVaultSettingsOperation.Apply(session, req.CoverImageBytes);
        return new UpdateVaultSettingsResponse { Success = true, NewVaultPath = newPath };
    }

    public async Task<UnlockVaultResponse> UnlockAsync(UnlockVaultRequest req, CancellationToken ct)
    {
        var session = await manager.UnlockAsync(req.FilePath, req.Password);
        return new UnlockVaultResponse { DisplayName = session.DisplayName };
    }

    public VaultOperationResponse Lock(LockVaultRequest req)
    {
        manager.Lock(req.FilePath);
        return new VaultOperationResponse { Success = true };
    }

    public async Task<VaultOperationResponse> ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        await manager.ChangePasswordAsync(req.FilePath, req.CurrentPassword, req.NewPassword);
        return new VaultOperationResponse { Success = true };
    }
}
