using System.Net;
using System.Net.Http.Json;
using FileVault.Service.Crypto;
using FileVault.Service.VaultOperations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FileVault.Web.Tests;

public class VaultRouteTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private string _vaultPath = null!;
    private const string TestToken = "vault-route-test-token";

    public VaultRouteTests()
    {
        Environment.SetEnvironmentVariable("VAULT_TOKEN", TestToken);
        Environment.SetEnvironmentVariable("ARGON2_FAST", "1");
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"));
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Vault-Token", TestToken);
        _vaultPath = Path.GetTempFileName() + ".vault";
        var manager = _factory.Services.GetRequiredService<VaultManager>();
        await manager.CreateVaultAsync(_vaultPath, "Test Vault", "password",
            argon2Params: KeyDerivation.FastParams);
    }

    public async Task DisposeAsync()
    {
        var manager = _factory.Services.GetRequiredService<VaultManager>();
        manager.Lock(_vaultPath);
        if (File.Exists(_vaultPath)) File.Delete(_vaultPath);
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("VAULT_TOKEN", null);
        Environment.SetEnvironmentVariable("ARGON2_FAST", null);
    }

    [Fact]
    public async Task Request_WithoutToken_Returns401()
    {
        using var noTokenClient = _factory.CreateClient();
        var res = await noTokenClient.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unlock_CorrectPassword_ReturnsDisplayName()
    {
        var res = await _client.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "password" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Test Vault", body!["displayName"]);
    }

    [Fact]
    public async Task Unlock_WrongPassword_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Lock_AfterUnlock_ReturnsOk()
    {
        await _client.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "password" });
        var res = await _client.PostAsJsonAsync("/api/vault/lock",
            new { path = _vaultPath });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_NewVault_ReturnsDisplayName()
    {
        var newPath = Path.GetTempFileName() + ".vault";
        try
        {
            var res = await _client.PostAsJsonAsync("/api/vault/create",
                new { path = newPath, displayName = "New Vault", password = "secret" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.Equal("New Vault", body!["displayName"]);
        }
        finally
        {
            var manager = _factory.Services.GetRequiredService<VaultManager>();
            manager.Lock(newPath);
            if (File.Exists(newPath)) File.Delete(newPath);
        }
    }

    [Fact]
    public async Task Create_ExistingPath_Returns409()
    {
        var res = await _client.PostAsJsonAsync("/api/vault/create",
            new { path = _vaultPath, displayName = "X", password = "y" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_ReturnsOk()
    {
        await _client.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "password" });
        var res = await _client.PostAsJsonAsync("/api/vault/change-password",
            new { path = _vaultPath, currentPassword = "password", newPassword = "newpass" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        // Verify new password works
        var unlock = await _client.PostAsJsonAsync("/api/vault/unlock",
            new { path = _vaultPath, password = "newpass" });
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/vault/change-password",
            new { path = _vaultPath, currentPassword = "wrong", newPassword = "new" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
