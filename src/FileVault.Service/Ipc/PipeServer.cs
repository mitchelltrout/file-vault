// src/FileVault.Service/Ipc/PipeServer.cs
using System.Buffers.Binary;
using System.IO.Pipes;
using MessagePack;
using FileVault.Shared.Ipc;
using FileVault.Shared.Ipc.Messages;

namespace FileVault.Service.Ipc;

public class PipeServer(string pipeName, MessageDispatcher dispatcher)
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(pipeName,
                PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(ct);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync();
                break;
            }
            _ = HandleClientAsync(pipe, ct);
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(pipe, ct);
                if (message is null) break;
                var response = await dispatcher.DispatchAsync(message, ct);
                if (response is not null)
                    await WriteMessageAsync(pipe, response, ct);
            }
        }
        finally { await pipe.DisposeAsync(); }
    }

    public static async Task<PipeMessage?> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        var lenBytes = new byte[4];
        try { await stream.ReadExactlyAsync(lenBytes, ct); }
        catch (EndOfStreamException) { return null; }

        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBytes);
        var payload = new byte[len];
        await stream.ReadExactlyAsync(payload, ct);
        return MessagePackSerializer.Deserialize<PipeMessage>(payload);
    }

    public static async Task WriteMessageAsync(Stream stream, PipeMessage message, CancellationToken ct)
    {
        var payload = MessagePackSerializer.Serialize(message);
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBytes, payload.Length);
        await stream.WriteAsync(lenBytes, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }
}
