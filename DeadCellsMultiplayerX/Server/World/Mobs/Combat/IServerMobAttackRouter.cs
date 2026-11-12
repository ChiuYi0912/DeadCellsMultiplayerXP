namespace DeadCellsMultiplayerX.Server.World.Mobs.Combat;

using DeadCellsMultiplayerX.Contracts.MobBehavior;

/// <summary>
/// 服务端 mob 攻击释放路由
/// 接收修改器产生的技能事件，并在服务端 Tick 中统一转发
/// </summary>
public interface IServerMobAttackRouter
{
    /// <summary>
    /// 接收一次 mob 技能准备事件，客户端据此播放对应技能
    /// </summary>
    public void SubmitSkill(MobModifierEvent evt);

    /// <summary>
    /// 推进攻击执行，返回本 Tick 产生的事件
    /// </summary>
    public IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs);

    /// <summary>
    /// 世界重置
    /// </summary>
    public void Clear();
}
