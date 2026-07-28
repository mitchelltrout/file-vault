// src/FileVault.Service/Ipc/Handlers/FileHandlers.cs
using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;
using FileVault.Shared.Ipc.Messages;

namespace FileVault.Service.Ipc.Handlers;

public class FileHandlers(VaultManager manager)
{
    public ListFolderResponse ListFolder(ListFolderRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");

        var nodes = session!.Tree.ListFolder(req.FolderPath)
            .Select(n => new VfsNodeDto
            {
                Name = n.Name,
                IsDirectory = n.IsDirectory,
                PlaintextLength = n.PlaintextLength,
                ModifiedAtUtc = n.ModifiedAtUtc,
                RotationDegrees = n.RotationDegrees
            }).ToList();
        return new ListFolderResponse { Nodes = nodes };
    }

    public async Task<FileOperationResponse> ImportAsync(ImportFilesRequest req, CancellationToken ct)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");

        var collision = Enum.Parse<CollisionBehavior>(req.CollisionBehavior);
        foreach (var sourcePath in req.SourcePaths)
        {
            ct.ThrowIfCancellationRequested();
            if (Directory.Exists(sourcePath))
                await ImportOperation.ImportDirectoryAsync(session!, req.TargetVaultFolder, sourcePath, collision, ct);
            else if (File.Exists(sourcePath))
                await ImportOperation.ImportFileAsync(session!, req.TargetVaultFolder, sourcePath, collision, ct);
        }
        return new FileOperationResponse { Success = true };
    }

    public async Task<FileOperationResponse> ExportAsync(ExportRequest req, CancellationToken ct)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        await ExportOperation.ExportAsync(session!, req.VaultNodePath, req.DestinationDirectory, ct);
        return new FileOperationResponse { Success = true };
    }

    public ReadFileResponse ReadFile(ReadFileRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        var data = ReadFileOperation.Read(session!, req.VaultNodePath, req.MaxBytes);
        return new ReadFileResponse { Data = data };
    }

    public FileOperationResponse Delete(DeleteRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        DeleteOperation.Delete(session!, req.VaultNodePath);
        return new FileOperationResponse { Success = true };
    }

    public FileOperationResponse Rename(RenameRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        RenameOperation.Rename(session!, req.VaultNodePath, req.NewName);
        return new FileOperationResponse { Success = true };
    }

    public FileOperationResponse Move(MoveRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        MoveOperation.Move(session!, req.SourcePath, req.DestinationFolder);
        return new FileOperationResponse { Success = true };
    }

    public FileOperationResponse CreateFolder(CreateFolderRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        CreateFolderOperation.CreateFolder(session!, req.FolderPath);
        return new FileOperationResponse { Success = true };
    }

    public ReadFileRangeResponse ReadFileRange(ReadFileRangeRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");
        var bytes = ReadFileRangeOperation.Read(session!, req.VaultNodePath, req.Offset, req.Length);
        return new ReadFileRangeResponse { Bytes = bytes };
    }

    public SetRotationResponse SetRotation(SetRotationRequest req)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            throw new InvalidOperationException("Vault is not unlocked.");

        using var _ = session!.Lock.WriteLock();
        var node = session.Tree.Find(req.VaultNodePath)
            ?? throw new FileNotFoundException($"Not found: {req.VaultNodePath}");
        node.RotationDegrees = req.RotationDegrees % 360;
        VaultContainer.VaultContainerIo.RewriteIndex(session.Stream, session.Key, session.Tree);
        return new SetRotationResponse { Success = true };
    }
}
