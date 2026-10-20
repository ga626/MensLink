using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;

namespace MensLink.Core;

// 这是本产品内部只读 IPC；对外 MCP 由官方 SDK 实现。
public static class ReadPipe
{
    private const int MaxBytes = 128 * 1024;
    public static string Name(string libraryId) => "menslink-p1-" + Guid.Parse(libraryId).ToString("N");

    public static async Task<string> Call(ReadRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.LibraryId, out _)) return Wire.Serialize(Wire.Error("invalid_argument"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(".", Name(request.LibraryId), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(timeout.Token);
            await Write(pipe, Wire.Serialize(request), timeout.Token);
            return await Read(pipe, timeout.Token);
        }
        catch (OperationCanceledException) { return Wire.Serialize(Wire.Error("service_unavailable")); }
        catch (IOException) { return Wire.Serialize(Wire.Error("service_unavailable")); }
    }

    public static async Task Serve(string libraryId, Func<ReadRequest, string> handler, CancellationToken stop)
    {
        // 单实例 fail closed；不连接网络，不接受写命令。
        while (!stop.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(Name(libraryId), PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(stop);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var request = Wire.Parse<ReadRequest>(await Read(server, timeout.Token));
                if (request.LibraryId != libraryId) throw new ContractException("not_authorized");
                await Write(server, handler(request), timeout.Token);
            }
            catch (Exception error) when (error is ContractException or System.Text.Json.JsonException or DecoderFallbackException)
            {
                try
                {
                    await Write(server, Wire.Serialize(Wire.Error(error is ContractException ce ? ce.Code : "invalid_argument")), timeout.Token);
                }
                catch (Exception disconnected) when (disconnected is IOException or OperationCanceledException) { }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException) { }
        }
    }

    private static async Task<string> Read(Stream stream, CancellationToken stop)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, stop);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaxBytes) throw new ContractException("invalid_argument");
        byte[] data = new byte[length];
        await stream.ReadExactlyAsync(data, stop);
        return new UTF8Encoding(false, true).GetString(data);
    }

    private static async Task Write(Stream stream, string text, CancellationToken stop)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        if (data.Length > MaxBytes) throw new ContractException("budget_too_small");
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);
        await stream.WriteAsync(header, stop);
        await stream.WriteAsync(data, stop);
        await stream.FlushAsync(stop);
    }
}
