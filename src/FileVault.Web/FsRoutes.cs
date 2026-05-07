public static class FsRoutes
{
    public static void MapFsRoutes(this WebApplication app)
    {
        app.MapGet("/api/fs/list", List);
    }

    private static IResult List(string? path)
    {
        var dir = string.IsNullOrEmpty(path)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : path;

        if (!Path.IsPathRooted(dir))
            return Results.BadRequest(new { error = "Path must be absolute." });

        if (!Directory.Exists(dir))
            return Results.BadRequest(new { error = "Directory not found." });

        try
        {
            var di = new DirectoryInfo(dir);
            var dirs = di.EnumerateDirectories()
                .Where(d => !d.Name.StartsWith('.') && (d.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => d.Name)
                .ToList();

            var vaultFiles = di.EnumerateFiles("*.vault")
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.Name)
                .ToList();

            return Results.Ok(new
            {
                path = di.FullName,
                parent = di.Parent?.FullName,
                dirs,
                vaultFiles,
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Json(new { error = "Access denied." }, statusCode: 403);
        }
    }
}
