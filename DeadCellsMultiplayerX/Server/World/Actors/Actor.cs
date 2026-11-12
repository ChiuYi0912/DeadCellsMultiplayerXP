namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc.pr;

/// <summary>
/// 一个客户端的 Ghost 聚合
/// </summary>
internal sealed class Actor : IDisposable
{
    private uint? appliedSpawnVersion;

    public Actor(string ownerGuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);
        this.OwnerGuid = ownerGuid;
    }

    /// <summary>
    /// 客户端 id
    /// </summary>
    public string OwnerGuid { get; }

    /// <summary>
    /// 当前服务端代表实体,创建失败时为空
    /// </summary>
    public ServerHeroCombatProxy? Ghost { get; private set; }

    /// <summary>
    /// ActionId 去重状态
    /// </summary>
    public Admission Admission { get; } = new();

    /// <summary>
    /// 同关卡复用；任何关卡变化都销毁旧实例并重建。
    /// </summary>
    /// <returns>是否创建了新的 ServerHeroCombatProxy</returns>
    public bool EnsureGhost(
        Game game,
        Level level,
        int cx = 0,
        int cy = 0)
    {
        if (this.Ghost != null && this.Ghost.MatchesLevel(level))
        {
            return false;
        }

        this.Ghost?.Dispose();
        this.Ghost = null;

        this.appliedSpawnVersion = null;
        this.Ghost = ServerHeroCombatProxy.CreateServer(
            game, level, this.OwnerGuid, cx, cy);
        return this.Ghost != null;
    }

    /// <summary>
    /// 判断 spawn 版本是否尚未应用
    /// </summary>
    public bool NeedsSpawn(uint version)
        => this.appliedSpawnVersion != version;

    /// <summary>
    /// 记录已应用的 spawn 版本
    /// </summary>
    public void MarkSpawnApplied(uint version)
        => this.appliedSpawnVersion = version;

    public void Dispose()
    {
        this.Ghost?.Dispose();
        this.Ghost = null;
    }
}

/// <summary>
/// 单个客户端的 ActionId 去重状态
/// </summary>
internal sealed class Admission
{
    private long lastActionId;

    /// <summary>
    /// 客户端传入的递增ActionId
    /// </summary>
    public bool IsNewAction(long actionId)
        => actionId > this.lastActionId;

    /// <summary>
    /// ServerHeroCombatProxy 创建成功后写入 ActionId 水位
    /// </summary>
    public void Commit(long actionId) => this.lastActionId = actionId;
}
