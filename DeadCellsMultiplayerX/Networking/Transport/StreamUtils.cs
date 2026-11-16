namespace DeadCellsMultiplayerX.Networking.Transport;

using System.Diagnostics;
using System.Text;
using Nerdbank.Streams;
using PolyType.SourceGenerator;
using StreamJsonRpc;

internal static class StreamUtils
{
    public static Stream CreateMultiplexingStream(this Stream stream, out MultiplexingStream result)
    {
        result = MultiplexingStream.Create(stream, new()
        {
            ProtocolMajorVersion = 3,
            SeededChannels =
            {
                new()
            }
        });
        return result.AcceptChannel(0).AsStream();
    }

    public static JsonRpc CreateJsonRpc(
        this Stream stream, string role = "unknown", TraceListener? failureListener = null)
        => stream.CreateJsonRpc(role, failureListener, out _, out _);

    /// <summary>Exposes the transport resources to endpoints that own their full lifetime.</summary>
    public static JsonRpc CreateJsonRpc(
        this Stream stream, string role, TraceListener? failureListener,
        out MultiplexingStream multiplexingStream, out TraceSource traceSource)
    {
        traceSource = new TraceSource("JSON-RPC", SourceLevels.Error);
        if (failureListener != null)
        {
            traceSource.Listeners.Add(failureListener);
        }

        MultiplexingStream? createdMux = null;
        LengthHeaderMessageHandler? handler = null;
        try
        {
            var rpcChannel = stream.CreateMultiplexingStream(out createdMux);
            var formatter = new NerdbankMessagePackFormatter
            {
                MultiplexingStream = createdMux,
                TypeShapeProvider = TypeShapeProvider_DeadCellsMultiplayerX.Default,
            };
            handler = new LengthHeaderMessageHandler(rpcChannel, rpcChannel, formatter);
            var rpc = new JsonRpc(handler) { TraceSource = traceSource };
            multiplexingStream = createdMux;
            return rpc;
        }
        catch
        {
            try
            {
                stream.Dispose();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "Failed RPC input cleanup"); }
            _ = DisposeFailedRpcAsync(handler, createdMux, traceSource);
            throw;
        }
    }

    private static async Task DisposeFailedRpcAsync(
        LengthHeaderMessageHandler? handler, MultiplexingStream? mux, TraceSource trace)
    {
        try
        {
            if (handler != null)
            {
                await handler.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Failed RPC handler cleanup"); }

        try
        {
            if (mux != null)
            {
                await mux.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Failed RPC transport cleanup"); }

        try
        {
            if (mux != null)
            {
                await mux.Completion.ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Serilog.Log.Debug(ex, "RPC transport ended during failed initialization"); }

        try
        {
            trace.Close();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "Failed RPC trace cleanup"); }
    }


    public static async Task<string> ReadGuestGuidAsync(Stream stream)
    {
        const int guidLength = 36;
        var buf = new byte[guidLength];
        var read = 0;
        while (read < guidLength)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, guidLength - read))
                                .ConfigureAwait(false);
            if (n == 0)
            {
                throw new EndOfStreamException(
                    $"Stream ended while reading guest GUID ({read}/{guidLength} bytes).");
            }

            read += n;
        }
        return Encoding.UTF8.GetString(buf);
    }
}
