namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

using dc.en;

/// <summary>
/// 按 mob 实例 CLR 类型重写行为的基础接口
/// 注册器在 mob 创建时执行一次 <see cref="InitHook"/>，
/// 然后调用 <see cref="Attach"/>。
/// </summary>
internal interface IMobModifier
{
    /// <summary>
    /// 该修改器支持的 mob CLR 类型，例如 typeof(dc.en.mob.Zombie)
    /// </summary>
    public Type MobType { get; }

    /// <summary>
    /// 注册该 mob 实例需要监听的 hook
    /// 注册器保证每个 mob 只调用一次
    /// </summary>
    public void InitHook(IMobHookRegistry hooks);

    /// <summary>
    /// 依赖注入完成后绑定到具体 mob
    /// </summary>
    public void Attach(Mob mob, IMobModifierContext context);

    /// <summary>
    /// mob 被移除或世界重置时解绑
    /// </summary>
    public void Detach(Mob mob);
}

/// <summary>
/// 带具体 mob 子类约束的修改器接口
/// 注册器仍然以非泛型 <see cref="IMobModifier"/> 保存实例
/// </summary>
internal interface IMobModifier<in TMob> : IMobModifier
    where TMob : Mob
{
    /// <summary>
    /// 类型安全的绑定入口。
    /// </summary>
    public void Attach(TMob mob, IMobModifierContext context);

    /// <summary>
    /// 类型安全的解绑入口。
    /// </summary>
    public void Detach(TMob mob);
}
