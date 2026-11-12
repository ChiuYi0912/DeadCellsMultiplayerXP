namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

using dc.en;

/// <summary>
/// 一次 hook 调用的上下文
/// </summary>
/// <remarks>
/// 创建 hook 上下文
/// </remarks>
public sealed class MobHookContext(IMobModifierContext modifierContext, long tick)
{

    /// <summary>
    /// 当前 mob
    /// </summary>
    public Mob Mob { get; } = modifierContext.Mob;

    /// <summary>
    /// 当前 mob 的 NetId
    /// </summary>
    public uint NetId { get; } = modifierContext.NetId;

    /// <summary>
    /// 当前 mob 的 CLR 类型
    /// </summary>
    public Type MobType { get; } = modifierContext.MobType;

    /// <summary>
    /// 当前 hook 触发时间
    /// </summary>
    public long Tick { get; } = tick;

    /// <summary>
    /// 修改器绑定上下文，可继续取服务端/客户端依赖
    /// </summary>
    public IMobModifierContext ModifierContext { get; } = modifierContext;
}
