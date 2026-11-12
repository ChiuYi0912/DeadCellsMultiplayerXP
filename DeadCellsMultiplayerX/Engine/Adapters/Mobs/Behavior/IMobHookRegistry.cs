using dc;
using dc.hl.types;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.MobBehavior;

namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior
{
    /// <summary>
    /// 服务端/客户端通用 hook 回调
    /// </summary>
    internal delegate void MobHandler(MobHookContext context);

    /// <summary>
    /// 技能准备 hook 回调；目标以引擎实体表示，端侧自行解析所需视图。
    /// </summary>
    internal delegate void MobSkillPrepareHandler(MobHookContext context, OldSkill skill, Entity target, int? data);

    /// <summary>
    /// 技能释放
    /// </summary>
    internal delegate void MobSkillExecuteHandle(MobHookContext context, OldSkill skill);

    /// <summary>
    /// 服务端命中结果 hook；返回 true 表示已处理
    /// </summary>
    internal delegate bool MobAttackResultHandler(
        MobHookContext context, AttackData atk, Entity target);

    /// <summary>
    /// Mob 自定义事件 hook
    /// </summary>
    internal delegate void MobEventHandler(MobHookContext context, MobModifierEvent evt);

    /// <summary>
    /// Mob 初始化技能 hook
    /// </summary>
    internal delegate void MobInitSkillsHandler(
        MobHookContext context, ArrayObj oldSkills, ArrayObj skills);

    /// <summary>
    /// 可拦截 hook；返回 true 表示调用方跳过原版逻辑
    /// </summary>
    internal delegate bool MobBehaviourHandler(MobHookContext context);

    /// <summary>
    /// 客户端/服务端共享的 hook 注册表。
    /// 只包含两端语义一致、无端侧类型的回调。
    /// </summary>
    internal interface IMobHookRegistry
    {
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
        /// mob 初始化技能之后通知
        /// </summary>
        void OnInitSkills(MobInitSkillsHandler handler);

        /// <summary>
        /// mob 死亡通知
        /// </summary>
        void OnMobDied(MobHandler handler);
    }

    /// <summary>
    /// 服务端 mob hook：权威 Tick、技能与命中结果。
    /// </summary>
    internal interface IServerMobHooks : IMobHookRegistry
    {
        /// <summary>
        /// 服务端独立 Tick
        /// </summary>
        void OnServerTick(MobHandler handler);

        /// <summary>
        /// 技能准备通知
        /// </summary>
        void OnSkillPrepare(MobSkillPrepareHandler handler);

        /// <summary>
        /// 技能执行通知
        /// </summary>
        void OnSkillExecute(MobSkillExecuteHandle handler);

        /// <summary>
        /// 服务端命中结果拦截
        /// </summary>
        void OnAttackResult(MobAttackResultHandler handler);
    }

    /// <summary>
    /// 客户端 mob hook：表现更新与自定义事件。
    /// </summary>
    internal interface IClientMobHooks : IMobHookRegistry
    {
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

        /// <summary>
        /// 客户端技能事件接管判定；返回 true 表示修改器自行处理，
        /// 未注册或返回 false 时由注册器走统一释放
        /// </summary>
        void OnClientEventHandled(MobBehaviourHandler handler);
    }
}
