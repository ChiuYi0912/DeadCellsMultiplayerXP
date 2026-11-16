namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.pr;
using DeadCellsMultiplayerX.Simulation.Actions;

/// <summary>
/// 一个客户端在服务端的 Hero 聚合：身份、引擎体、装备镜像、命令账本与战斗视图。
/// </summary>
internal sealed class ServerHero : IDisposable
{
    private uint? appliedSpawnVersion;

    public ServerHero(string ownerGuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);
        this.OwnerGuid = ownerGuid;
    }

    /// <summary>
    /// 客户端 id（连接 GUID）
    /// </summary>
    public string OwnerGuid { get; }

    /// <summary>
    /// 当前服务端引擎体；创建失败或跨关卡时为 null
    /// </summary>
    public ServerHeroCombatProxy? Ghost { get; private set; }

    /// <summary>
    /// 服务端保存的装备快照；动作物品与 tiers 的解析来源。
    /// </summary>
    public HeroLoadoutMirror Loadout { get; } = new();

    /// <summary>
    /// 已处理 CommandId 的结果缓存。
    /// </summary>
    public CommandLedger Commands { get; } = new();

    /// <summary>
    /// 当前所在关卡；尚未创建引擎体时为 null。
    /// </summary>
    public Level? Level { get; private set; }

    /// <summary>
    /// 服务端镜像的客户端生命。
    /// </summary>
    public int Life { get; private set; }

    public int MaxLife { get; private set; }

    public bool IsDead { get; private set; }

    /// <summary>
    /// 是否允许 mob 选中。
    /// </summary>
    public bool CombatEnabled { get; private set; }

    /// <summary>
    /// 最近一次上传状态时间。
    /// </summary>
    public long LastStateMs { get; private set; }

    /// <summary>
    /// 底层可命中实体。
    /// </summary>
    public dc.Entity? Entity => this.Ghost?.Hero;

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
            this.Level = level;
            return false;
        }

        this.Ghost?.Dispose();
        this.Ghost = null;

        this.appliedSpawnVersion = null;
        this.Ghost = ServerHeroCombatProxy.CreateServer(
            game, level, this.OwnerGuid, cx, cy);
        this.Level = this.Ghost != null ? level : null;
        return this.Ghost != null;
    }

    /// <summary>
    /// 判断 spawn 版本是否尚未应用。
    /// </summary>
    public bool NeedsSpawn(uint version)
        => this.appliedSpawnVersion != version;

    /// <summary>
    /// 记录已应用的 spawn 版本。
    /// </summary>
    public void MarkSpawnApplied(uint version)
        => this.appliedSpawnVersion = version;

    /// <summary>
    /// 更新客户端镜像状态。
    /// </summary>
    public void UpdateCombat(
        Level level,
        int life,
        int maxLife,
        bool isDead,
        bool combatEnabled,
        long nowMs)
    {
        this.Level = level;
        this.Life = life;
        this.MaxLife = maxLife;
        this.IsDead = isDead;
        this.CombatEnabled = combatEnabled;
        this.LastStateMs = nowMs;
    }

    /// <summary>
    /// 关闭战斗状态；断线、跨关卡或服务端假人时使用。
    /// </summary>
    public void DisableCombat()
    {
        this.CombatEnabled = false;
        this.Ghost?.SetCombatEnabled(false);
    }

    public void Dispose()
    {
        this.Ghost?.Dispose();
        this.Ghost = null;
        this.Level = null;
        this.CombatEnabled = false;
    }
}
