using System.Security.Cryptography;
using System.Text;
using FileVault.Service.VaultOperations;

var token = Environment.GetEnvironmentVariable("VAULT_TOKEN")
    ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5000");
builder.Services.AddSingleton<VaultManager>();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        var headerToken = ctx.Request.Headers["X-Vault-Token"].FirstOrDefault() ?? "";
        var queryToken = ctx.Request.Query["token"].FirstOrDefault() ?? "";
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var headerOk = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(headerToken), tokenBytes);
        var queryOk = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(queryToken), tokenBytes);
        if (!headerOk && !queryOk)
        {
            ctx.Response.StatusCode = 401;
            return;
        }
    }
    await next(ctx);
});

app.UseStaticFiles();

app.MapGet("/", async (HttpContext ctx) =>
{
    var htmlPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    var html = await File.ReadAllTextAsync(htmlPath);
    html = html.Replace("{{VAULT_TOKEN}}", token);
    ctx.Response.ContentType = "text/html";
    await ctx.Response.WriteAsync(html);
});

app.MapVaultRoutes();
app.MapFileRoutes();

if (!app.Environment.IsEnvironment("Testing"))
{
    Console.WriteLine($"\nFileVault started.");
    Console.WriteLine($"Token: {token}");
    Console.WriteLine("Open http://localhost:5000 in your browser.\n");
}

app.Run();

public partial class Program { }
