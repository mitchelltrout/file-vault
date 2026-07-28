// src/FileVault.Service/Ipc/MessageDispatcher.cs
using FileVault.Service.VaultOperations;
using FileVault.Service.Ipc.Handlers;
using FileVault.Shared.Ipc;
using FileVault.Shared.Ipc.Messages;
using MessagePack;

namespace FileVault.Service.Ipc;

public class MessageDispatcher(VaultManager vaultManager)
{
    private readonly VaultHandlers _vault = new(vaultManager);
    private readonly FileHandlers _file = new(vaultManager);

    public async Task<PipeMessage?> DispatchAsync(PipeMessage message, CancellationToken ct)
    {
        try
        {
            return message.Type switch
            {
                MessageType.CreateVaultRequest => Respond(message,
                    await _vault.CreateAsync(Deserialize<CreateVaultRequest>(message), ct)),
                MessageType.UnlockVaultRequest => Respond(message,
                    await _vault.UnlockAsync(Deserialize<UnlockVaultRequest>(message), ct)),
                MessageType.LockVaultRequest => Respond(message,
                    _vault.Lock(Deserialize<LockVaultRequest>(message))),
                MessageType.ChangePasswordRequest => Respond(message,
                    await _vault.ChangePasswordAsync(Deserialize<ChangePasswordRequest>(message), ct)),
                MessageType.ListFolderRequest => Respond(message,
                    _file.ListFolder(Deserialize<ListFolderRequest>(message))),
                MessageType.ImportFilesRequest => Respond(message,
                    await _file.ImportAsync(Deserialize<ImportFilesRequest>(message), ct)),
                MessageType.ExportRequest => Respond(message,
                    await _file.ExportAsync(Deserialize<ExportRequest>(message), ct)),
                MessageType.DeleteRequest => Respond(message,
                    _file.Delete(Deserialize<DeleteRequest>(message))),
                MessageType.ReadFileRequest => Respond(message,
                    _file.ReadFile(Deserialize<ReadFileRequest>(message))),
                MessageType.RenameRequest => Respond(message,
                    _file.Rename(Deserialize<RenameRequest>(message))),
                MessageType.MoveRequest => Respond(message,
                    _file.Move(Deserialize<MoveRequest>(message))),
                MessageType.CreateFolderRequest => Respond(message,
                    _file.CreateFolder(Deserialize<CreateFolderRequest>(message))),
                MessageType.UpdateVaultSettingsRequest => Respond(message,
                    _vault.UpdateSettings(Deserialize<UpdateVaultSettingsRequest>(message))),
                MessageType.ReadFileRangeRequest => Respond(message,
                    _file.ReadFileRange(Deserialize<ReadFileRangeRequest>(message))),
                MessageType.SetRotationRequest => Respond(message,
                    _file.SetRotation(Deserialize<SetRotationRequest>(message))),
                _ => Error(message, $"Unknown message type: {message.Type}")
            };
        }
        catch (Exception ex)
        {
            return Error(message, ex.Message);
        }
    }

    private static T Deserialize<T>(PipeMessage m) =>
        MessagePackSerializer.Deserialize<T>(m.Payload);

    private static PipeMessage Respond<T>(PipeMessage req, T response) => new()
    {
        Type = (MessageType)(req.Type + 1),
        RequestId = req.RequestId,
        Payload = MessagePackSerializer.Serialize(response)
    };

    private static PipeMessage Error(PipeMessage req, string message) => new()
    {
        Type = MessageType.ErrorResponse,
        RequestId = req.RequestId,
        Payload = MessagePackSerializer.Serialize(new ErrorResponse { Message = message })
    };
}
