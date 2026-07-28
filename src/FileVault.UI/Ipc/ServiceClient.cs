// src/FileVault.UI/Ipc/ServiceClient.cs
using System.Buffers.Binary;
using System.IO.Pipes;
using MessagePack;
using FileVault.Shared.Ipc;
using FileVault.Shared.Ipc.Messages;

namespace FileVault.UI.Ipc;

public class ServiceClient : IServiceClient, IAsyncDisposable
{
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_pipe?.IsConnected == true) return;
        _pipe?.Dispose();
        _pipe = new NamedPipeClientStream(".", IpcConstants.PipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(5000, ct);
    }

    private async Task<T> SendReceiveAsync<TReq, T>(MessageType type, TReq request, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            await EnsureConnectedAsync(ct);
            var msg = new PipeMessage
            {
                Type = type,
                RequestId = Guid.NewGuid(),
                Payload = MessagePackSerializer.Serialize(request)
            };

            var msgBytes = MessagePackSerializer.Serialize(msg);
            var lenBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lenBytes, msgBytes.Length);
            await _pipe!.WriteAsync(lenBytes, ct);
            await _pipe.WriteAsync(msgBytes, ct);
            await _pipe.FlushAsync(ct);

            var respLenBytes = new byte[4];
            await _pipe.ReadExactlyAsync(respLenBytes, ct);
            var respLen = BinaryPrimitives.ReadInt32LittleEndian(respLenBytes);
            var respBytes = new byte[respLen];
            await _pipe.ReadExactlyAsync(respBytes, ct);
            var response = MessagePackSerializer.Deserialize<PipeMessage>(respBytes);

            if (response.Type == MessageType.ErrorResponse)
            {
                var err = MessagePackSerializer.Deserialize<ErrorResponse>(response.Payload);
                throw new InvalidOperationException(err.Message);
            }

            return MessagePackSerializer.Deserialize<T>(response.Payload);
        }
        finally { _sendLock.Release(); }
    }

    public async Task CreateVaultAsync(string filePath, string displayName, string password,
        byte[]? coverImageBytes = null, CancellationToken ct = default) =>
        await SendReceiveAsync<CreateVaultRequest, VaultOperationResponse>(
            MessageType.CreateVaultRequest,
            new CreateVaultRequest
            {
                FilePath = filePath,
                DisplayName = displayName,
                Password = password,
                CoverImageBytes = coverImageBytes ?? []
            }, ct);

    public async Task<string> UpdateVaultSettingsAsync(string vaultPath, byte[]? coverImageBytes, CancellationToken ct = default)
    {
        var resp = await SendReceiveAsync<UpdateVaultSettingsRequest, UpdateVaultSettingsResponse>(
            MessageType.UpdateVaultSettingsRequest,
            new UpdateVaultSettingsRequest { VaultPath = vaultPath, CoverImageBytes = coverImageBytes ?? [] }, ct);
        return resp.NewVaultPath;
    }

    public Task<UnlockVaultResponse> UnlockVaultAsync(string filePath, string password, CancellationToken ct = default) =>
        SendReceiveAsync<UnlockVaultRequest, UnlockVaultResponse>(
            MessageType.UnlockVaultRequest,
            new UnlockVaultRequest { FilePath = filePath, Password = password }, ct);

    public async Task LockVaultAsync(string filePath, CancellationToken ct = default) =>
        await SendReceiveAsync<LockVaultRequest, VaultOperationResponse>(
            MessageType.LockVaultRequest,
            new LockVaultRequest { FilePath = filePath }, ct);

    public async Task ChangePasswordAsync(string filePath, string currentPassword, string newPassword, CancellationToken ct = default) =>
        await SendReceiveAsync<ChangePasswordRequest, VaultOperationResponse>(
            MessageType.ChangePasswordRequest,
            new ChangePasswordRequest { FilePath = filePath, CurrentPassword = currentPassword, NewPassword = newPassword }, ct);

    public Task<ListFolderResponse> ListFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default) =>
        SendReceiveAsync<ListFolderRequest, ListFolderResponse>(
            MessageType.ListFolderRequest,
            new ListFolderRequest { VaultPath = vaultPath, FolderPath = folderPath }, ct);

    public async Task ImportFilesAsync(string vaultPath, string targetFolder, IEnumerable<string> sourcePaths,
        string collisionBehavior, CancellationToken ct = default) =>
        await SendReceiveAsync<ImportFilesRequest, FileOperationResponse>(
            MessageType.ImportFilesRequest,
            new ImportFilesRequest
            {
                VaultPath = vaultPath, TargetVaultFolder = targetFolder,
                SourcePaths = sourcePaths.ToList(), CollisionBehavior = collisionBehavior
            }, ct);

    public async Task ExportAsync(string vaultPath, string vaultNodePath, string destDir, CancellationToken ct = default) =>
        await SendReceiveAsync<ExportRequest, FileOperationResponse>(
            MessageType.ExportRequest,
            new ExportRequest { VaultPath = vaultPath, VaultNodePath = vaultNodePath, DestinationDirectory = destDir }, ct);

    public async Task DeleteAsync(string vaultPath, string vaultNodePath, CancellationToken ct = default) =>
        await SendReceiveAsync<DeleteRequest, FileOperationResponse>(
            MessageType.DeleteRequest,
            new DeleteRequest { VaultPath = vaultPath, VaultNodePath = vaultNodePath }, ct);

    public async Task<byte[]> ReadFileAsync(string vaultPath, string vaultNodePath, long maxBytes = 52_428_800, CancellationToken ct = default)
    {
        var resp = await SendReceiveAsync<ReadFileRequest, ReadFileResponse>(
            MessageType.ReadFileRequest,
            new ReadFileRequest { VaultPath = vaultPath, VaultNodePath = vaultNodePath, MaxBytes = maxBytes }, ct);
        return resp.Data;
    }

    public async Task RenameAsync(string vaultPath, string vaultNodePath, string newName, CancellationToken ct = default) =>
        await SendReceiveAsync<RenameRequest, FileOperationResponse>(
            MessageType.RenameRequest,
            new RenameRequest { VaultPath = vaultPath, VaultNodePath = vaultNodePath, NewName = newName }, ct);

    public async Task MoveAsync(string vaultPath, string sourcePath, string destFolder, CancellationToken ct = default) =>
        await SendReceiveAsync<MoveRequest, FileOperationResponse>(
            MessageType.MoveRequest,
            new MoveRequest { VaultPath = vaultPath, SourcePath = sourcePath, DestinationFolder = destFolder }, ct);

    public async Task CreateFolderAsync(string vaultPath, string folderPath, CancellationToken ct = default) =>
        await SendReceiveAsync<CreateFolderRequest, FileOperationResponse>(
            MessageType.CreateFolderRequest,
            new CreateFolderRequest { VaultPath = vaultPath, FolderPath = folderPath }, ct);

    public async Task<byte[]> ReadFileRangeAsync(string vaultPath, string vaultNodePath, long offset, int length, CancellationToken ct = default)
    {
        var resp = await SendReceiveAsync<ReadFileRangeRequest, ReadFileRangeResponse>(
            MessageType.ReadFileRangeRequest,
            new ReadFileRangeRequest
            {
                VaultPath = vaultPath,
                VaultNodePath = vaultNodePath,
                Offset = offset,
                Length = length,
            }, ct);
        return resp.Bytes;
    }

    public async Task SetRotationAsync(string vaultPath, string vaultNodePath, int rotationDegrees, CancellationToken ct = default) =>
        await SendReceiveAsync<SetRotationRequest, SetRotationResponse>(
            MessageType.SetRotationRequest,
            new SetRotationRequest { VaultPath = vaultPath, VaultNodePath = vaultNodePath, RotationDegrees = rotationDegrees }, ct);

    public async ValueTask DisposeAsync()
    {
        if (_pipe is not null) await _pipe.DisposeAsync();
    }
}
