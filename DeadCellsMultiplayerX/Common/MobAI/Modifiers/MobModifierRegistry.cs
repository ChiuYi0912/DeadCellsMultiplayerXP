using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using Serilog;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 按 mob CLR 类型注册修改器
    /// 服务端和客户端各持有一个实例
    /// </summary>
    public sealed class MobModifierRegistry<TContext>
        where TContext : IMobModifierContext
    {
        private sealed class Entry
        {
            public Entry(
                IMobModifier modifier,
                MobHookRegistry hooks,
                TContext context)
            {
                Modifier = modifier;
                Hooks = hooks;
                Context = context;
            }

            public IMobModifier Modifier { get; }
            public MobHookRegistry Hooks { get; }
            public TContext Context { get; }
            public bool LoggedAiLocked { get; set; }
        }

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobModifierRegistry");

        private readonly Dictionary<System.Type, Func<TContext, IMobModifier>> factories = [];

        /// <summary>
        /// 实例注册
        /// </summary>
        private readonly Dictionary<Mob, Entry> active = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Entity.__uid 注册实例 
        /// </summary>
        private readonly Dictionary<int, Entry> activeByUid = [];


        /// <summary>
        /// 注册一个 mob 类型的修改器工厂
        /// 工厂接收绑定上下文，可在这里完成构造注入
        /// </summary>
        public void Register(System.Type mobType, Func<TContext, IMobModifier> factory)
        {
            if (mobType == null || factory == null)
                return;

            factories[mobType] = factory;
        }

        /// <summary>
        /// 泛型注册入口，按 CLR 类型匹配，不依赖引擎字符串 id
        /// </summary>
        public void Register<TMob>(Func<TContext, IMobModifier> factory)
            where TMob : Mob
            => Register(typeof(TMob), factory);

        /// <summary>
        /// 为上下文中的 mob 创建并绑定修改器
        /// 返回 true 表示该 mob 已被某个修改器接管
        /// </summary>
        public bool TryAttach(TContext context)
        {
            if (context == null || context.Mob == null)
                return false;

            var mob = context.Mob;
            if (mob.destroyed)
                return false;

            if (TryGetEntry(mob, out _))
                return true;

            System.Type mobType = mob.GetType();
            if (!factories.TryGetValue(mobType, out var factory))
                return false;

            IMobModifier? modifier = factory(context);
            var hooks = new MobHookRegistry();

            if (modifier == null)
                return false;

            modifier.InitHook(hooks);
            modifier.Attach(mob, context);

            var entry = new Entry(modifier, hooks, context);
            active[mob] = entry;
            if (mob.__uid != 0)
                activeByUid[mob.__uid] = entry;

            Logger.Debug(
                "Mob modifier attached: mob={Mob} type={MobType} netId={NetId} modifier={Modifier}",
                mob.GetType().Name, mobType.Name, context.NetId, modifier.GetType().Name);
            return true;
        }

        /// <summary>
        /// 解绑指定 mob
        /// </summary>
        public void Detach(Mob mob)
        {
            if (mob == null)
                return;

            Entry? entry = null;
            if (active.Remove(mob, out var byRef))
            {
                entry = byRef;
            }
            else if (mob.__uid != 0
                && activeByUid.Remove(mob.__uid, out var byUid))
            {
                entry = byUid;
                active.Remove(byUid.Context.Mob);
            }
            else
            {
                return;
            }

            if (mob.__uid != 0)
                activeByUid.Remove(mob.__uid);

            entry.Modifier.Detach(mob);
        }

        /// <summary>
        /// 查询 mob 是否由修改器接管
        /// </summary>
        public bool TryGet(Mob mob, out IMobModifier modifier)
        {
            if (TryGetEntry(mob, out var entry))
            {
                modifier = entry.Modifier;
                return true;
            }

            modifier = null!;
            return false;
        }

        /// <summary>
        /// 拦截原版 behaviourAi；返回 true 时调用方跳过 orig
        /// </summary>
        public bool DispatchBehaviourAi(Mob mob, long tick)
            => TryGetEntry(mob, out var entry)
                && entry.Hooks.DispatchBehaviourAi(CreateContext(entry, tick));

        /// <summary>
        /// 拦截原版 fixedUpdate；返回 true 时调用方跳过 orig
        /// </summary>
        public bool DispatchFixedUpdateHandled(Mob mob, long tick)
            => TryGetEntry(mob, out var entry)
                && entry.Hooks.DispatchFixedUpdateHandled(
                    CreateContext(entry, tick));

        /// <summary>
        /// 原版 preUpdate 通知
        /// </summary>
        public void DispatchPreUpdate(Mob mob, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchPreUpdate(CreateContext(entry, tick));
        }

        /// <summary>
        /// 原版 fixedUpdate 通知
        /// </summary>
        public void DispatchFixedUpdate(Mob mob, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchFixedUpdate(CreateContext(entry, tick));
        }

        /// <summary>
        /// 原版 postUpdate 通知
        /// </summary>
        public void DispatchPostUpdate(Mob mob, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchPostUpdate(CreateContext(entry, tick));
        }

        /// <summary>
        /// 技能准备通知
        /// </summary>
        public void DispatchSkillPrepare(Mob mob, OldSkill skill, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchSkillPrepare(
                    CreateContext(entry, tick), skill);
        }

        /// <summary>
        /// 技能执行通知
        /// </summary>
        public void DispatchSkillExecute(Mob mob, OldSkill skill, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchSkillExecute(
                    CreateContext(entry, tick), skill);
        }

        /// <summary>
        /// mob 死亡通知
        /// </summary>
        public void DispatchMobDied(Mob mob, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchMobDied(CreateContext(entry, tick));
        }

        /// <summary>
        /// 服务端命中结果拦截；返回 true 时调用方跳过原版逻辑
        /// </summary>
        public bool DispatchAttackResult(
            Mob mob, AttackData atk, Entity target, long tick)
            => TryGetEntry(mob, out var entry)
                && entry.Hooks.DispatchAttackResult(
                    CreateContext(entry, tick), atk, target);

        /// <summary>
        /// 客户端 AI 锁定拦截；null 表示没有修改器处理
        /// </summary>
        public bool? DispatchAiLocked(Mob mob, long tick)
        {
            if (!TryGetEntry(mob, out var entry))
                return null;

            bool? result = entry.Hooks.DispatchAiLocked(
                CreateContext(entry, tick));

            if (result.HasValue && !entry.LoggedAiLocked)
            {
                entry.LoggedAiLocked = true;
            }

            return result;
        }

        /// <summary>
        /// 客户端每帧更新拦截；返回 true 时调用方跳过默认表现
        /// </summary>
        public bool DispatchClientUpdate(Mob mob, long tick)
            => TryGetEntry(mob, out var entry)
                && entry.Hooks.DispatchClientUpdate(
                    CreateContext(entry, tick));

        /// <summary>
        /// 客户端自定义事件
        /// </summary>
        public void DispatchClientEvent(
            Mob mob, MobModifierEvent evt, long tick)
        {
            if (TryGetEntry(mob, out var entry))
                entry.Hooks.DispatchClientEvent(
                    CreateContext(entry, tick), evt);
        }

        /// <summary>
        /// 服务端独立 Tick；只调用已绑定修改器
        /// </summary>
        public void Tick(long tick)
        {
            if (active.Count == 0)
                return;

            foreach (var entry in new List<Entry>(active.Values))
            {
                var context = CreateContext(entry, tick);
                entry.Hooks.DispatchServerTick(context);
            }
        }

        /// <summary>
        /// 清空全部绑定；用于 epoch/世界重置
        /// </summary>
        public void Clear()
        {
            foreach (var (mob, entry) in new List<KeyValuePair<Mob, Entry>>(active))
            {
                active.Remove(mob);
                entry.Modifier.Detach(mob);
            }

            active.Clear();
            activeByUid.Clear();
        }

        private bool TryGetEntry(Mob mob, out Entry entry)
        {
            if (mob != null)
            {
                if (active.TryGetValue(mob, out var byRef))
                {
                    entry = byRef;
                    return true;
                }

                if (mob.__uid != 0
                    && activeByUid.TryGetValue(mob.__uid, out var byUid))
                {
                    entry = byUid;
                    return true;
                }
            }

            entry = null!;
            return false;
        }

        private static MobHookContext CreateContext(Entry entry, long tick) => new(entry.Context, tick);
    }
}
