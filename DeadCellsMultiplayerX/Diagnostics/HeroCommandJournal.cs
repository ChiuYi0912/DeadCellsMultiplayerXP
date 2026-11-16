namespace DeadCellsMultiplayerX.Diagnostics;

/// <summary>
/// 服务端动作处理的有界环形日志。
/// 只记录入口/结果/耗时，不参与玩法逻辑；用于定位“动作没执行/被执行两次”的问题。
/// </summary>
internal sealed class HeroCommandJournal
{
    private const int CAPACITY = 256;

    private readonly Queue<Entry> entries = new(CAPACITY);

    public void Record(Entry entry)
    {
        while (this.entries.Count >= CAPACITY)
        {
            _ = this.entries.Dequeue();
        }

        this.entries.Enqueue(entry);
    }

    public void Clear() => this.entries.Clear();

    public void Dump(TextWriter writer, int count = 32)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var take = Math.Clamp(count, 1, CAPACITY);
        var snapshot = this.entries.ToArray();
        var start = Math.Max(0, snapshot.Length - take);

        for (var i = start; i < snapshot.Length; i++)
        {
            var entry = snapshot[i];
            writer.WriteLine(
                $"{entry.ServerTimeMs} {entry.SenderGuid} " +
                $"cmd={entry.CommandId} kind={entry.Kind} slot={entry.Slot} " +
                $"result={entry.Result} {entry.ElapsedMs:F2}ms");
        }
    }

    internal readonly record struct Entry(
        long ServerTimeMs,
        string SenderGuid,
        int Epoch,
        long CommandId,
        string Kind,
        int Slot,
        string Result,
        double ElapsedMs);
}
