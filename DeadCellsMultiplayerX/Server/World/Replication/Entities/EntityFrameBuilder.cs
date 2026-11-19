namespace DeadCellsMultiplayerX.Server.World.Replication.Entities;

using Serilog;

/// <summary>
/// 服务端通用实体状态发布器
/// 负责首次静态数据序列化与每 tick 的动态状态采集
/// </summary>
internal sealed class EntityFrameBuilder(
    ServerEntityRegistry registry,
    ServerEntityHandlerRegistry handlers)
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "EntityFrameBuilder");

    private readonly ServerEntityRegistry registry = registry;
    private readonly ServerEntityHandlerRegistry handlers = handlers;

    /// <summary>
    /// 30Hz 实体位置历史，供服务端武器命中回退使用
    /// </summary>
    public ServerEntityHistory History { get; } = new();

    /// <summary>
    /// 采集本 tick 的生成、状态与移除数据
    /// </summary>
    public EntityFrameSnapshot Collect(long serverTimeMs)
    {
        var result = new EntityFrameSnapshot();

        foreach (var entry in this.registry.Entries)
        {
            if (!this.handlers.TryGet(entry.Kind, out var handler))
            {
                Logger.Debug(
                    "No entity handler for kind={Kind} netId={NetId}",
                    entry.Kind, entry.NetId);
                continue;
            }

            if (!handler.IsAlive(entry))
            {
                continue;
            }

            if (!entry.SpawnSerialized && !handler.TryPrepareSpawn(entry))
            {
                continue;
            }

            if (!entry.SpawnBroadcast)
            {
                result.Spawned.Add(entry.Spawn);
            }

            var dynamicState = handler.BuildDynamic(entry, serverTimeMs);
            if (dynamicState != null)
            {
                result.Entities.Add(dynamicState);
            }
        }

        result.Despawned.AddRange(this.registry.GetRemoved());

        return result;
    }

    /// <summary>
    /// 世界帧成功构建后，确认生成/移除数据并记录历史。
    /// </summary>
    public void Commit(long serverTimeMs, EntityFrameSnapshot result)
    {
        foreach (var spawn in result.Spawned)
        {
            if (this.registry.TryGet(spawn.NetId, out var entry))
            {
                entry.SpawnBroadcast = true;
            }
        }
        this.registry.CommitRemoved(result.Despawned);
        this.History.Record(serverTimeMs, result.Entities);
    }

    /// <summary>
    /// 构建全量重同步数据
    /// </summary>
    public EntityFrameSnapshot BuildResync(long serverTimeMs)
    {
        var result = new EntityFrameSnapshot();

        foreach (var entry in this.registry.Entries)
        {
            if (!entry.SpawnSerialized)
            {
                continue;
            }

            if (!this.handlers.TryGet(entry.Kind, out var handler))
            {
                continue;
            }

            if (!handler.IsAlive(entry))
            {
                continue;
            }

            result.Spawned.Add(entry.Spawn);

            var dynamicState = handler.BuildDynamic(entry, serverTimeMs);
            if (dynamicState != null)
            {
                result.Entities.Add(dynamicState);
            }
        }

        return result;
    }
}
