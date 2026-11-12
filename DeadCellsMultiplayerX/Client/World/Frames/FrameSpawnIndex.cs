using DeadCellsMultiplayerX.Contracts.Snapshots;

namespace DeadCellsMultiplayerX.Client.World.Frames
{
    /// <summary>
    /// 远端英雄静态数据索引；由世界帧与重同步基线共同维护。
    /// </summary>
    internal sealed class FrameSpawnIndex
    {
        private readonly Dictionary<string, HeroSpawn> spawns = [];

        public void Add(HeroSpawn? spawn)
        {
            if (spawn != null)
                spawns[spawn.GUID] = spawn;
        }

        public void AddRange(IEnumerable<HeroSpawn>? entries)
        {
            if (entries == null) return;

            foreach (var spawn in entries)
                Add(spawn);
        }

        public void Clear() => spawns.Clear();

        public bool TryGet(string guid, out HeroSpawn spawn)
            => spawns.TryGetValue(guid, out spawn!);
    }
}
