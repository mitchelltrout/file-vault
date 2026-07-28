// tests/FileVault.Service.Tests/Ipc/PipeIntegrationTests.cs
using FileVault.Service.Ipc;
using FileVault.Service.VaultOperations;
using FileVault.Shared.Ipc;
using FileVault.Shared.Ipc.Messages;
using FluentAssertions;

namespace FileVault.Service.Tests.Ipc;

public class PipeIntegrationTests : IAsyncDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly VaultManager _manager = new();
    private readonly string _pipeName = "FileVaultTest_" + Guid.NewGuid().ToString("N")[..8];
    private PipeServer? _server;
    private CancellationTokenSource? _cts;

    public PipeIntegrationTests() => Directory.CreateDirectory(_tempDir);

    private async Task StartServer()
    {
        _cts = new CancellationTokenSource();
        var dispatcher = new MessageDispatcher(_manager);
        _server = new PipeServer(_pipeName, dispatcher);
        _ = _server.RunAsync(_cts.Token);
        await Task.Delay(100); // let server start
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _manager.Dispose();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task CreateAndUnlock_ViaNamedPipe()
    {
        await StartServer();
        using var client = new PipeClient(_pipeName);
        await client.ConnectAsync();

        var vaultPath = Path.Combine(_tempDir, "pipe.vault");
        await client.SendAsync(MessageType.CreateVaultRequest,
            new CreateVaultRequest { FilePath = vaultPath, DisplayName = "Pipe Vault", Password = "pass" });
        var createResp = await client.ReceiveAsync<VaultOperationResponse>();
        createResp.Success.Should().BeTrue();

        await client.SendAsync(MessageType.UnlockVaultRequest,
            new UnlockVaultRequest { FilePath = vaultPath, Password = "pass" });
        var unlockResp = await client.ReceiveAsync<UnlockVaultResponse>();
        unlockResp.DisplayName.Should().Be("Pipe Vault");
    }
}
