namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.pr;

/// <summary>
/// 为一个客户端 Ghost 建立可战斗视图
/// </summary>
internal sealed class ServerHeroCombatView(string guid, ServerHeroCombatProxy ghost, Level level)
{

    /// <summary>
    /// 客户端 id
    /// </summary>
    public string Guid { get; } = guid;

    /// <summary>
    /// Ghost 生命周期壳
    /// </summary>
    public ServerHeroCombatProxy Ghost { get; } = ghost;

    /// <summary>
    /// 当前所在关卡
    /// </summary>
    public Level Level { get; private set; } = level;

    /// <summary>
    /// 服务端镜像的客户端生命
    /// </summary>
    public int Life { get; private set; }

    /// <summary>
    /// 服务端镜像的客户端最大生命
    /// </summary>
    public int MaxLife { get; private set; }

    /// <summary>
    /// 客户端是否已死亡
    /// </summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// 是否允许 mob 选中
    /// </summary>
    public bool CombatEnabled { get; private set; }

    /// <summary>
    /// 最近一次上传状态时间
    /// </summary>
    public long LastStateMs { get; private set; }

    /// <summary>
    /// 底层可命中实体
    /// </summary>
    public Entity Entity => this.Ghost.Hero;

    /// <summary>
    /// 更新客户端镜像状态
    /// </summary>
    public void Update(
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
    /// 关闭战斗状态
    /// </summary>
    public void DisableCombat() => this.CombatEnabled = false;
}
