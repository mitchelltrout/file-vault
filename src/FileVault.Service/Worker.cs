// src/FileVault.Service/Worker.cs
using FileVault.Service.Ipc;
using FileVault.Service.VaultOperations;
using FileVault.Shared.Ipc;

namespace FileVault.Service;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FileVault Service starting.");
        var manager = new VaultManager();
        var dispatcher = new MessageDispatcher(manager);
        var server = new PipeServer(IpcConstants.PipeName, dispatcher);
        logger.LogInformation("Listening on pipe: {PipeName}", IpcConstants.PipeName);
        await server.RunAsync(stoppingToken);
        logger.LogInformation("FileVault Service stopped.");
    }
}
