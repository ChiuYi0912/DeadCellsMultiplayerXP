using System.Diagnostics;
using System.Text;
namespace DeadCellsMultiplayerX.Diagnostics
{
    /// <summary>
    /// 记录 JSON-RPC 发送失败的文件监听器
    /// </summary>
    internal sealed class JsonRpcFailureTraceListener : TraceListener
    {
        private readonly string filePath;
        private readonly object gate = new();
        private StreamWriter? writer;
        private bool disposed;

        public JsonRpcFailureTraceListener(string filePath)
        {
            this.filePath = filePath;
        }

        public override void Write(string? message)
            => WriteIfFailure(TraceEventType.Error, 0, message);

        public override void WriteLine(string? message)
            => WriteIfFailure(TraceEventType.Error, 0, message);

        public override void TraceEvent(
            TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, string? message)
            => WriteIfFailure(eventType, id, message);

        public override void TraceEvent(
            TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, string? format,
            params object?[]? args)
        {
            string? message = format == null
                ? null
                : args == null || args.Length == 0
                    ? format
                    : string.Format(format, args);

            WriteIfFailure(eventType, id, message);
        }

        public override void TraceData(
            TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, object? data)
            => WriteIfFailure(eventType, id, data?.ToString());

        public override void TraceData(
            TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, params object?[]? data)
        {
            if (data == null || data.Length == 0) return;

            foreach (var item in data)
                WriteIfFailure(eventType, id, item?.ToString());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;

            if (disposing)
            {
                lock (gate)
                {
                    if (writer != null)
                    {
                        writer.Flush();
                        writer.Dispose();
                        writer = null;
                    }
                }
            }

            base.Dispose(disposing);
        }

        private void WriteIfFailure(
            TraceEventType eventType, int id, string? message)
        {
            if (disposed || string.IsNullOrEmpty(message)) return;
            if (!IsSendFailure(message)) return;
            if (IsDisconnect(message)) return;

            lock (gate)
            {
                EnsureWriter();

                writer!.Write('[');
                writer.Write(DateTime.Now.ToString("HH:mm:ss.fff"));
                writer.Write("] ");
                writer.Write(eventType);
                writer.Write(" id=");
                writer.Write(id);
                writer.Write("  ");
                writer.WriteLine(message);
            }
        }

        private void EnsureWriter()
        {
            if (writer != null) return;

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                System.IO.Directory.CreateDirectory(directory);

            writer = new StreamWriter(
                new FileStream(
                    filePath, FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite),
                Encoding.UTF8)
            {
                AutoFlush = true,
            };

            writer.WriteLine(
                $"# JSON-RPC failure log opened at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        }

        private static bool IsSendFailure(string message)
        {
            return message.Contains(
                       "Error writing JSON RPC", StringComparison.Ordinal)
                || message.Contains(
                       "Error sending JSON RPC", StringComparison.Ordinal)
                || message.Contains(
                       "Failed to send JSON RPC", StringComparison.Ordinal);
        }

        private static bool IsDisconnect(string message)
        {
            return message.Contains(
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
    }
}
