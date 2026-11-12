namespace DeadCellsMultiplayerX.Diagnostics;

using System.Diagnostics;
using System.Text;

/// <summary>
/// 记录 JSON-RPC 发送失败的文件监听器
/// </summary>
internal sealed class JsonRpcFailureTraceListener(string filePath) : TraceListener
{
    private readonly string filePath = filePath;
    private readonly Lock gate = new();
    private StreamWriter? writer;
    private bool disposed;

    public override void Write(string? message)
        => this.WriteIfFailure(TraceEventType.Error, 0, message);

    public override void WriteLine(string? message)
        => this.WriteIfFailure(TraceEventType.Error, 0, message);

    public override void TraceEvent(
        TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
        => this.WriteIfFailure(eventType, id, message);

    public override void TraceEvent(
        TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? format,
        params object?[]? args)
    {
        var message = format == null
            ? null
            : args == null || args.Length == 0
                ? format
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    format,
                    args);

        this.WriteIfFailure(eventType, id, message);
    }

    public override void TraceData(
        TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, object? data)
        => this.WriteIfFailure(eventType, id, data?.ToString());

    public override void TraceData(
        TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, params object?[]? data)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        foreach (var item in data)
        {
            this.WriteIfFailure(eventType, id, item?.ToString());
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        if (disposing)
        {
            lock (this.gate)
            {
                if (this.writer != null)
                {
                    this.writer.Flush();
                    this.writer.Dispose();
                    this.writer = null;
                }
            }
        }

        base.Dispose(disposing);
    }

    private void WriteIfFailure(
        TraceEventType eventType, int id, string? message)
    {
        if (this.disposed || string.IsNullOrEmpty(message))
        {
            return;
        }

        if (!IsSendFailure(message))
        {
            return;
        }

        if (IsDisconnect(message))
        {
            return;
        }

        lock (this.gate)
        {
            this.EnsureWriter();

            this.writer!.Write('[');
            this.writer.Write(DateTime.Now.ToString(
                "HH:mm:ss.fff",
                System.Globalization.CultureInfo.InvariantCulture));
            this.writer.Write("] ");
            this.writer.Write(eventType);
            this.writer.Write(" id=");
            this.writer.Write(id);
            this.writer.Write("  ");
            this.writer.WriteLine(message);
        }
    }

    private void EnsureWriter()
    {
        if (this.writer != null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(this.filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        this.writer = new StreamWriter(
            new FileStream(
                this.filePath, FileMode.Append,
                FileAccess.Write, FileShare.ReadWrite),
            Encoding.UTF8)
        {
            AutoFlush = true,
        };

        this.writer.WriteLine(
            $"# JSON-RPC failure log opened at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
    }

    private static bool IsSendFailure(string message) => message.Contains(
                   "Error writing JSON RPC", StringComparison.Ordinal)
            || message.Contains(
                   "Error sending JSON RPC", StringComparison.Ordinal)
            || message.Contains(
                   "Failed to send JSON RPC", StringComparison.Ordinal);

    private static bool IsDisconnect(string message) => message.Contains(
                   "connection with the remote party was lost",
                   StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection lost",
                   StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection is closed",
                   StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection was closed",
                   StringComparison.OrdinalIgnoreCase)
            || message.Contains("aborted",
                   StringComparison.OrdinalIgnoreCase)
            || message.Contains("disconnected",
                   StringComparison.OrdinalIgnoreCase);
}
