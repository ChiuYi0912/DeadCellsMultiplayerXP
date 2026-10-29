using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 服务端/客户端通用 hook 回调
    /// </summary>
    public delegate void MobHandler(MobHookContext context);

    /// <summary>
    /// 技能相关 hook 回调
    /// </summary>
    public delegate void MobSkillHandler(MobHookContext context, OldSkill skill);

    /// <summary>
    /// 服务端命中结果 hook；返回 true 表示已处理
    /// </summary>
    public delegate bool MobAttackResultHandler(
        MobHookContext context, AttackData atk, Entity target);

    /// <summary>
    /// Mob 自定义事件 hook
    /// </summary>
    public delegate void MobEventHandler(
        MobHookContext context, MobModifierEvent evt);

    /// <summary>
    /// 可拦截 hook；返回 true 表示调用方跳过原版逻辑
    /// </summary>
    public delegate bool MobBehaviourHandler(MobHookContext context);

    /// <summary>
    /// 修改器的 hook 注册表
    /// 每个 mob 实例一个，由注册器注入 <see cref="IMobModifier.InitHook"/>。
    /// </summary>
    public interface IMobHookRegistry
    {
        /// <summary>
        /// 服务端独立 Tick
        /// </summary>
        void OnServerTick(MobHandler handler);

        /// <summary>
        /// 原版 preUpdate 前后通知
        /// </summary>
        void OnPreUpdate(MobHandler handler);

        /// <summary>
        /// 原版 fixedUpdate 通知（不拦截）
        /// </summary>
        void OnFixedUpdate(MobHandler handler);

        /// <summary>
        /// 原版 postUpdate 通知
        /// </summary>
        void OnPostUpdate(MobHandler handler);

        /// <summary>
        /// 拦截 behaviourAi；返回 true 时跳过原版 AI
        /// </summary>
        void OnBehaviourAi(MobBehaviourHandler handler);

        /// <summary>
        /// 拦截 fixedUpdate；返回 true 时完全接管移动/物理/命中
        /// </summary>
        void OnFixedUpdateHandled(MobBehaviourHandler handler);

        /// <summary>
        /// 技能准备通知
        /// </summary>
        void OnSkillPrepare(MobSkillHandler handler);

        /// <summary>
        /// 技能执行通知
        /// </summary>
        void OnSkillExecute(MobSkillHandler handler);

        /// <summary>
        /// mob 死亡通知
        /// </summary>
        void OnMobDied(MobHandler handler);

        /// <summary>
        /// 服务端命中结果拦截
        /// </summary>
        void OnAttackResult(MobAttackResultHandler handler);

        /// <summary>
        /// 客户端 AI 锁定拦截
        /// </summary>
        void OnAiLocked(MobBehaviourHandler handler);

        /// <summary>
        /// 客户端每帧更新拦截；返回 true 时调用方跳过默认表现
        /// </summary>
        void OnClientUpdate(MobBehaviourHandler handler);

        /// <summary>
        /// 客户端自定义事件
        /// </summary>
        void OnClientEvent(MobEventHandler handler);
    }
}
