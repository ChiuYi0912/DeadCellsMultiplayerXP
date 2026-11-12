namespace DeadCellsMultiplayerX.Server.Replication.Entities;

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
    /// 20Hz 实体位置历史，供服务端武器命中回退使用
    /// </summary>
    public ServerEntityHistory History { get; } = new();

    /// <summary>
    /// 采集本 tick 的生成、状态与移除数据
    /// </summary>
    public EntityPublishResult Collect(long serverTimeMs)
    {
        var result = new EntityPublishResult();

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
                entry.SpawnBroadcast = true;
            }

            var dynamicState = handler.BuildDynamic(entry, serverTimeMs);
            if (dynamicState != null)
            {
                result.Entities.Add(dynamicState);
            }
        }

        result.Despawned.AddRange(this.registry.DrainRemoved());
        this.History.Record(serverTimeMs, result.Entities);

        return result;
    }

    /// <summary>
    /// 构建全量重同步数据
    /// </summary>
    public EntityPublishResult BuildResync(long serverTimeMs)
    {
        var result = new EntityPublishResult();

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
