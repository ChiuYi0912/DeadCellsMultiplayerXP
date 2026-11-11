using System;
using System.Collections.Generic;

namespace DeadCellsMultiplayerX.Common.Diagnostics
{
    public readonly record struct ActionJournalEntry(
        long Sequence,
        long ServerTimeMs,
        string SenderGuid,
        string Kind,
        int Slot,
        bool Accepted,
        string Rejection,
        long ClientTimeMs,
        string LevelId);

    public sealed class ActionJournal
    {
        public const int DefaultCapacity = 2048;

        private readonly object gate = new();
        private readonly ActionJournalEntry[] entries;
        private readonly Dictionary<string, long> byKind = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> byRejection = new(StringComparer.Ordinal);

        private int next;
        private int count;
        private long sequence;
        private long total;
        private long accepted;
        private long rejected;

        public ActionJournal(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            entries = new ActionJournalEntry[capacity];
        }

        /// <summary>
        /// 记录一次已被接受或拒绝的动作
        /// </summary>
        public void Record(
            string senderGuid,
            string kind,
            int slot,
            bool isAccepted,
            string rejection,
            long clientTimeMs,
            long serverTimeMs,
            string levelId)
        {
            lock (gate)
            {
                string safeKind = kind ?? string.Empty;
                string safeRejection = rejection ?? string.Empty;

                entries[next] = new ActionJournalEntry(
                    ++sequence,
                    serverTimeMs,
                    senderGuid ?? string.Empty,
                    safeKind,
                    slot,
                    isAccepted,
                    safeRejection,
                    clientTimeMs,
                    levelId ?? string.Empty);

                next = (next + 1) % entries.Length;
                if (count < entries.Length)
                    count++;

                total++;
                if (isAccepted)
                {
                    accepted++;
                }
                else
                {
                    rejected++;
                    byRejection[safeRejection] =
                        byRejection.TryGetValue(safeRejection, out long reasonCount)
                            ? reasonCount + 1
                            : 1;
                }

                byKind[safeKind] =
                    byKind.TryGetValue(safeKind, out long kindCount)
                        ? kindCount + 1
                        : 1;
            }
        }

        public ActionJournalSnapshot Snapshot()
        {
            lock (gate)
            {
                var recent = new ActionJournalEntry[count];
                int start = count == 0
                    ? 0
                    : (next - count + entries.Length) % entries.Length;

                for (int i = 0; i < count; i++)
                    recent[i] = entries[(start + i) % entries.Length];

                return new ActionJournalSnapshot
                {
                    Total = total,
                    Accepted = accepted,
                    Rejected = rejected,
                    ByKind = new Dictionary<string, long>(byKind, StringComparer.Ordinal),
                    ByRejection = new Dictionary<string, long>(byRejection, StringComparer.Ordinal),
                    Recent = recent,
                };
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                Array.Clear(entries);
                next = 0;
                count = 0;
                sequence = 0;
                total = 0;
                accepted = 0;
                rejected = 0;
                byKind.Clear();
                byRejection.Clear();
            }
        }
    }

    public sealed class ActionJournalSnapshot
    {
        public long Total { get; init; }
        public long Accepted { get; init; }
        public long Rejected { get; init; }
        public IReadOnlyDictionary<string, long> ByKind { get; init; } =
            new Dictionary<string, long>();
        public IReadOnlyDictionary<string, long> ByRejection { get; init; } =
            new Dictionary<string, long>();
        public IReadOnlyList<ActionJournalEntry> Recent { get; init; } =
            Array.Empty<ActionJournalEntry>();
    }
}
