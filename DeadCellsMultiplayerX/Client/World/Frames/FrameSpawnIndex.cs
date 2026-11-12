namespace DeadCellsMultiplayerX.Client.World.Frames;

using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 远端英雄静态数据索引；由世界帧与重同步基线共同维护。
/// </summary>
internal sealed class FrameSpawnIndex
{
    private readonly Dictionary<string, HeroSpawn> spawns = [];

    public void Add(HeroSpawn? spawn)
    {
        if (spawn != null)
        {
            this.spawns[spawn.GUID] = spawn;
        }
    }

    public void AddRange(IEnumerable<HeroSpawn>? entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (var spawn in entries)
        {
            this.Add(spawn);
        }
    }

    public void Clear() => this.spawns.Clear();

    public bool TryGet(string guid, out HeroSpawn spawn)
        => this.spawns.TryGetValue(guid, out spawn!);
}
