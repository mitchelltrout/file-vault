using FileVault.Service.Crypto;
using FileVault.Service.VaultOperations;

public static class VaultRoutes
{
    public static void MapVaultRoutes(this WebApplication app)
    {
        var g = app.MapGroup("/api/vault");
        g.MapPost("/create", CreateVault);
        g.MapPost("/unlock", Unlock);
        g.MapPost("/lock", Lock);
        g.MapPost("/change-password", ChangePassword);
    }

    private static Argon2Params? GetArgon2Params() =>
        Environment.GetEnvironmentVariable("ARGON2_FAST") == "1"
            ? KeyDerivation.FastParams
            : null;

    private static async Task<IResult> CreateVault(CreateVaultRequest req, VaultManager manager)
    {
        if (File.Exists(req.Path))
            return Results.Conflict(new { error = "A file already exists at that path." });
        try
        {
            await manager.CreateVaultAsync(req.Path, req.DisplayName, req.Password,
                argon2Params: GetArgon2Params(), coverImageBytes: null);
            var session = await manager.UnlockAsync(req.Path, req.Password,
                argon2Params: GetArgon2Params());
            return Results.Ok(new { displayName = session.DisplayName });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> Unlock(UnlockRequest req, VaultManager manager)
    {
        try
        {
            var session = await manager.UnlockAsync(req.Path, req.Password,
                argon2Params: GetArgon2Params());
            return Results.Ok(new { displayName = session.DisplayName });
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Results.Json(new { error = "Wrong password." }, statusCode: 401);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static IResult Lock(LockRequest req, VaultManager manager)
    {
        manager.Lock(req.Path);
        return Results.Ok();
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest req, VaultManager manager)
    {
        try
        {
            await manager.ChangePasswordAsync(req.Path, req.CurrentPassword, req.NewPassword,
                argon2Params: GetArgon2Params());
            return Results.Ok();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Results.Json(new { error = "Wrong current password." }, statusCode: 401);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}

public record CreateVaultRequest(string Path, string DisplayName, string Password);
public record UnlockRequest(string Path, string Password);
public record LockRequest(string Path);
public record ChangePasswordRequest(string Path, string CurrentPassword, string NewPassword);
