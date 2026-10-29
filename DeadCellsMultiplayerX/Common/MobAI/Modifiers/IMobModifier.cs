using System;
using dc.en;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 按 mob 实例 CLR 类型重写行为的基础接口
    /// 注册器在 mob 创建时执行一次 <see cref="InitHook"/>，
    /// 然后调用 <see cref="Attach"/>。
    /// </summary>
    public interface IMobModifier
    {
        /// <summary>
        /// 该修改器支持的 mob CLR 类型，例如 typeof(dc.en.mob.Zombie)
        /// </summary>
        Type MobType { get; }

        /// <summary>
        /// 注册该 mob 实例需要监听的 hook
        /// 注册器保证每个 mob 只调用一次
        /// </summary>
        void InitHook(IMobHookRegistry hooks);

        /// <summary>
        /// 依赖注入完成后绑定到具体 mob
        /// </summary>
        void Attach(Mob mob, IMobModifierContext context);

        /// <summary>
        /// mob 被移除或世界重置时解绑
        /// </summary>
        void Detach(Mob mob);
    }

    /// <summary>
    /// 带具体 mob 子类约束的修改器接口
    /// 注册器仍然以非泛型 <see cref="IMobModifier"/> 保存实例
    /// </summary>
    public interface IMobModifier<in TMob> : IMobModifier
        where TMob : Mob
    {
        /// <summary>
        /// 类型安全的绑定入口。
        /// </summary>
        void Attach(TMob mob, IMobModifierContext context);

        /// <summary>
        /// 类型安全的解绑入口。
        /// </summary>
        void Detach(TMob mob);
    }
}
