// tests/FileVault.Service.Tests/Ipc/PipeClient.cs
using System.IO.Pipes;
using MessagePack;
using FileVault.Service.Ipc;
using FileVault.Shared.Ipc;
using FileVault.Shared.Ipc.Messages;

namespace FileVault.Service.Tests.Ipc;

public class PipeClient(string pipeName) : IDisposable
{
    private readonly NamedPipeClientStream _pipe = new(".", pipeName,
        PipeDirection.InOut, PipeOptions.Asynchronous);

    public Task ConnectAsync() => _pipe.ConnectAsync(5000);

    public Task SendAsync<T>(MessageType type, T payload)
    {
        var msg = new PipeMessage
        {
            Type = type,
            RequestId = Guid.NewGuid(),
            Payload = MessagePackSerializer.Serialize(payload)
        };
        return PipeServer.WriteMessageAsync(_pipe, msg, CancellationToken.None);
    }

    public async Task<T> ReceiveAsync<T>()
    {
        var msg = await PipeServer.ReadMessageAsync(_pipe, CancellationToken.None);
        if (msg?.Type == MessageType.ErrorResponse)
        {
            var err = MessagePackSerializer.Deserialize<ErrorResponse>(msg.Payload);
            throw new Exception($"Service error: {err.Message}");
        }
        return MessagePackSerializer.Deserialize<T>(msg!.Payload);
    }

    public void Dispose() => _pipe.Dispose();
}
