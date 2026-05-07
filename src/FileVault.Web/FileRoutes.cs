using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;

public static class FileRoutes
{
    public static void MapFileRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/files");
        g.MapGet("/list", List);
        g.MapGet("/stream", Stream);
        g.MapPost("/import", Import);
        g.MapGet("/export", Export);
        g.MapPost("/mkdir", MkDir);
        g.MapDelete("/", Delete);
        g.MapPost("/rename", Rename);
        g.MapPost("/move", Move);
    }

    private static IResult List(string vaultPath, string path, VaultManager manager)
    {
        if (!manager.TryGetSession(vaultPath, out var session))
            return Results.StatusCode(403);

        var items = session!.Tree.ListFolder(path);
        var result = items.Select(n => new
        {
            name = n.Name,
            isDirectory = n.IsDirectory,
            size = n.IsDirectory ? 0L : n.PlaintextLength,
            modifiedAt = DateTimeOffset.FromUnixTimeSeconds(n.ModifiedAtUtc).ToString("O"),
        });
        return Results.Ok(result);
    }

    private static IResult Stream(string vaultPath, string path, HttpContext ctx,
        VaultManager manager)
    {
        if (!manager.TryGetSession(vaultPath, out var session))
            return Results.StatusCode(403);

        var node = session!.Tree.Find(path);
        if (node is null || node.IsDirectory)
            return Results.NotFound();

        var contentType = GetContentType(node.Name);
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers.AcceptRanges = "bytes";

        var rangeHeader = ctx.Request.Headers.Range.ToString();
        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
        {
            var parts = rangeHeader["bytes=".Length..].Split('-');
            long rangeStart = long.TryParse(parts[0], out var s) ? s : 0;
            long rangeEndInclusive = parts.Length > 1 && long.TryParse(parts[1], out var e)
                ? Math.Min(e, node.PlaintextLength - 1)
                : node.PlaintextLength - 1;
            var requestedLength = (int)(rangeEndInclusive - rangeStart + 1);

            var data = ReadFileRangeOperation.Read(session, path, rangeStart, requestedLength);

            ctx.Response.StatusCode = 206;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength = data.Length;
            ctx.Response.Headers.ContentRange =
                $"bytes {rangeStart}-{rangeStart + data.Length - 1}/{node.PlaintextLength}";
            return Results.Bytes(data, contentType);
        }

        var fullData = ReadFileOperation.Read(session, path, long.MaxValue);
        ctx.Response.ContentLength = fullData.Length;
        return Results.Bytes(fullData, contentType);
    }

    private static string GetContentType(string filename)
    {
        return Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static async Task<IResult> Import(
        HttpRequest req, VaultManager manager, CancellationToken ct)
    {
        if (!req.HasFormContentType)
            return Results.BadRequest(new { error = "Expected multipart/form-data." });

        var form = await req.ReadFormAsync(ct);
        var vaultPath = form["vaultPath"].FirstOrDefault();
        var folder = form["folder"].FirstOrDefault() ?? "/";

        if (string.IsNullOrEmpty(vaultPath))
            return Results.BadRequest(new { error = "vaultPath is required." });
        if (!manager.TryGetSession(vaultPath, out var session))
            return Results.StatusCode(403);

        const int ChunkSize = 1024 * 1024;
        foreach (var file in form.Files)
        {
            // Buffer the upload without holding the vault lock (avoids lock during network I/O)
            using var ms = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            var fileId = new byte[16];
            System.Security.Cryptography.RandomNumberGenerator.Fill(fileId);
            var chunks = new List<FileVault.Service.VirtualFileSystem.ChunkRef>();
            long totalPlaintext = 0;

            using var lk = session!.Lock.WriteLock();
            var buffer = new byte[ChunkSize];
            int chunkIndex = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var n = ms.Read(buffer, 0, ChunkSize);
                if (n == 0) break;
                var plain = n == ChunkSize ? buffer : buffer.AsSpan(0, n).ToArray();
                var enc = FileVault.Service.Crypto.AesGcmChunked.EncryptChunk(
                    session.Key, plain, fileId, chunkIndex);
                var off = FileVault.Service.VaultContainer.VaultContainerIo.AppendChunkAt(
                    session.Stream, enc);
                chunks.Add(new FileVault.Service.VirtualFileSystem.ChunkRef
                {
                    ContainerOffset = off,
                    CiphertextLength = n,
                    PlaintextLength = n,
                });
                totalPlaintext += n;
                chunkIndex++;
                if (n < ChunkSize) break;
            }

            var vaultFilePath = folder.TrimEnd('/') + "/" + file.FileName;
            session.Tree.UpsertFileChunked(vaultFilePath, fileId, chunks, totalPlaintext);
            FileVault.Service.VaultContainer.VaultContainerIo.RewriteIndex(
                session.Stream, session.Key, session.Tree);
        }

        return Results.Ok();
    }

    private static IResult Export(string vaultPath, string path, VaultManager manager)
    {
        if (!manager.TryGetSession(vaultPath, out var session))
            return Results.StatusCode(403);

        var node = session!.Tree.Find(path);
        if (node is null || node.IsDirectory)
            return Results.NotFound();

        // ReadFileOperation acquires session.Lock internally — do NOT acquire it again
        var data = ReadFileOperation.Read(session, path, long.MaxValue);
        return Results.File(data, "application/octet-stream", fileDownloadName: node.Name);
    }

    private static IResult MkDir(MkDirRequest req, VaultManager manager)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            return Results.StatusCode(403);
        using var lk = session!.Lock.WriteLock();
        CreateFolderOperation.CreateFolder(session, req.Path);
        return Results.Ok();
    }

    private static IResult Delete(string vaultPath, string path, VaultManager manager)
    {
        if (!manager.TryGetSession(vaultPath, out var session))
            return Results.StatusCode(403);
        try
        {
            using var lk = session!.Lock.WriteLock();
            DeleteOperation.Delete(session, path);
            return Results.Ok();
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static IResult Rename(RenameRequest req, VaultManager manager)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            return Results.StatusCode(403);
        try
        {
            using var lk = session!.Lock.WriteLock();
            RenameOperation.Rename(session, req.Path, req.NewName);
            return Results.Ok();
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static IResult Move(MoveRequest req, VaultManager manager)
    {
        if (!manager.TryGetSession(req.VaultPath, out var session))
            return Results.StatusCode(403);
        try
        {
            using var lk = session!.Lock.WriteLock();
            MoveOperation.Move(session, req.SourcePath, req.DestFolder);
            return Results.Ok();
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }
}

public record MkDirRequest(string VaultPath, string Path);
public record RenameRequest(string VaultPath, string Path, string NewName);
public record MoveRequest(string VaultPath, string SourcePath, string DestFolder);
