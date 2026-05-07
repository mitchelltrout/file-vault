using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FileVault.Service.Crypto;
using FileVault.Service.FileOperations;
using FileVault.Service.VaultOperations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FileVault.Web.Tests;

public class FileRouteTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private string _vaultPath = null!;
    private VaultManager _manager = null!;
    private const string TestToken = "file-route-test-token";

    public FileRouteTests()
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
        _manager = _factory.Services.GetRequiredService<VaultManager>();

        await _manager.CreateVaultAsync(_vaultPath, "Test", "pass",
            argon2Params: KeyDerivation.FastParams);
        await _manager.UnlockAsync(_vaultPath, "pass",
            argon2Params: KeyDerivation.FastParams);

        var session = _manager.GetSession(_vaultPath);
        var tmpFile = Path.Combine(Path.GetTempPath(), "filevault_test_hello.txt");
        await File.WriteAllTextAsync(tmpFile, "hello world");
        await ImportOperation.ImportFileAsync(session, "/", tmpFile,
            CollisionBehavior.Replace, CancellationToken.None);
        File.Delete(tmpFile);
    }

    public async Task DisposeAsync()
    {
        _manager.Lock(_vaultPath);
        if (File.Exists(_vaultPath)) File.Delete(_vaultPath);
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("VAULT_TOKEN", null);
        Environment.SetEnvironmentVariable("ARGON2_FAST", null);
    }

    private string V => Uri.EscapeDataString(_vaultPath);

    [Fact]
    public async Task List_RootFolder_ContainsImportedFile()
    {
        var res = await _client.GetAsync($"/api/files/list?vaultPath={V}&path=/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.NotNull(body);
        Assert.Contains(body, f => f["name"].ToString() == "filevault_test_hello.txt");
    }

    [Fact]
    public async Task List_NoSession_Returns403()
    {
        var res = await _client.GetAsync("/api/files/list?vaultPath=/nonexistent.vault&path=/");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Stream_ExistingFile_ReturnsContent()
    {
        var path = Uri.EscapeDataString("/filevault_test_hello.txt");
        var res = await _client.GetAsync($"/api/files/stream?vaultPath={V}&path={path}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Equal("hello world", content);
    }

    [Fact]
    public async Task Stream_WithRange_Returns206()
    {
        var path = Uri.EscapeDataString("/filevault_test_hello.txt");
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/files/stream?vaultPath={V}&path={path}");
        req.Headers.Range = new RangeHeaderValue(0, 4); // "hello"
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.PartialContent, res.StatusCode);
        var content = await res.Content.ReadAsStringAsync();
        Assert.Equal("hello", content);
    }

    [Fact]
    public async Task Stream_HasNoCacheHeader()
    {
        var path = Uri.EscapeDataString("/filevault_test_hello.txt");
        var res = await _client.GetAsync($"/api/files/stream?vaultPath={V}&path={path}");
        Assert.True(res.Headers.TryGetValues("Cache-Control", out var vals)
            && vals.Any(v => v.Contains("no-store")));
    }
}
