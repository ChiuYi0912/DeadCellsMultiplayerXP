using dc.en;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.MobBehavior.Events;
using DeadCellsMultiplayerX.Client.MobBehavior.Modifiers.Shield;
using DeadCellsMultiplayerX.Client.MobBehavior.Modifiers.Zombie;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Client.MobBehavior.Modifiers
{
    /// <summary>
    /// 客户端 mob 修改器注册器
    /// </summary>
    internal sealed class ClientMobModifierRegistry
    {
        private readonly GuestClientSession session;
        private readonly RemoteWorld world;
        private readonly IClientMobEventBus events;
        private readonly MobModifierRegistry<ClientMobModifierContext> inner = new();

        public ClientMobModifierRegistry(
            GuestClientSession session,
            RemoteWorld world,
            IClientMobEventBus events)
        {
            this.session = session;
            this.world = world;
            this.events = events;

            inner.Register<GameZombie>(
                ctx => new ZombieClientModifier(ctx.Session, ctx.World));
            inner.Register<dc.en.mob.Shield>(
                ctx => new ShieldClientModifier(ctx.Session, ctx.World));
        }

        /// <summary>
        /// 注册一个客户端 mob 修改器工厂
        /// </summary>
        public void Register(
            System.Type mobType,
            Func<ClientMobModifierContext, IMobModifier> factory)
            => inner.Register(mobType, factory);

        /// <summary>
        /// 为客户端 mob 绑定修改器
        /// </summary>
        public bool TryAttach(Mob mob, long tick)
        {
            if (mob == null
                || !world.Mobs.Receiver.TryGetNetId(mob, out uint netId))
                return false;

            var context = new ClientMobModifierContext(
                mob, netId, tick, session, world, events, world.Ghosts);

            return inner.TryAttach(context);
        }

        /// <summary>
        /// 解绑指定 mob
        /// </summary>
        public void Detach(Mob mob) => inner.Detach(mob);

        /// <summary>
        /// 查询 mob 是否由修改器接管
        /// </summary>
        public bool TryGet(Mob mob, out IMobModifier modifier)
            => inner.TryGet(mob, out modifier);

        /// <summary>
        /// 客户端 AI 锁定拦截；null 表示没有修改器处理
        /// </summary>
        public bool? DispatchAiLocked(Mob mob, long tick)
            => inner.DispatchAiLocked(mob, tick);

        /// <summary>
        /// 客户端每帧更新拦截；返回 true 时调用方跳过默认表现
        /// </summary>
        public bool DispatchClientUpdate(Mob mob, long tick)
            => inner.DispatchClientUpdate(mob, tick);

        /// <summary>
        /// 客户端自定义事件：注册过修改器的 mob 交给修改器；
        /// 其余 mob 统一按技能 id 释放
        /// </summary>
        public void DispatchClientEvent(
            Mob mob, MobModifierEvent evt, long tick)
        {
            if (inner.TryGet(mob, out _))
            {
                bool handled = inner.DispatchClientEventHandled(mob, tick);

                inner.DispatchClientEvent(mob, evt, tick);

                if (!handled)
                    ReleaseSkillEvent(mob, evt, tick);

                return;
            }

            ReleaseSkillEvent(mob, evt, tick);
        }

        /// <summary>
        /// 未注册修改器的 mob 统一释放收到的技能事件
        /// </summary>
        public void ReleaseSkillEvent(Mob mob, MobModifierEvent evt, long tick)
        {
            if (mob == null || mob.destroyed || evt == null)
                return;

            if (evt.Kind != MobModifierEventKind.ActionStarted
                || string.IsNullOrEmpty(evt.SkillId))
                return;

            var hero = ResolveTargetHero(evt.TargetGuid);
            if (hero == null || hero.destroyed)
                return;

            mob.setAttackTarget(hero);
            mob.setNemesisTarget(hero);

#if DEBUG
            hero.maxLife = 9999;
            hero.life = 9999;
#endif

            var skills = mob.oldSkills;
            if (skills == null)
                return;

            foreach (var item in skills)
            {
                if (item is not OldMobSkill mobSkill)
                    continue;

                if (mobSkill.id.ToString() != evt.SkillId)
                    continue;

                mobSkill.prepareOnOwnerTarget(true, null);
            }
        }

        private Hero? ResolveTargetHero(string guid)
        {
            if (!string.IsNullOrEmpty(guid)
                && world.Ghosts.TryGet(guid, out var ghost)
                && ghost.Hero != null
                && !ghost.Hero.destroyed)
            {
                ghost.SetPlaceholderVitals();
                return ghost.Hero;
            }

            return session.Game?.hero;
        }

        /// <summary>
        /// mob 初始化技能后分发
        /// </summary>
        public void DispatchInitSkills(
            Mob mob, dc.hl.types.ArrayObj oldSkills,
            dc.hl.types.ArrayObj skills, long tick)
            => inner.DispatchInitSkills(mob, oldSkills, skills, tick);

        public void OnMobFixedUpdate(Mob mob, long tick)
            => inner.DispatchFixedUpdate(mob, tick);

        /// <summary>
        /// 清空全部绑定。
        /// </summary>
        public void Clear() => inner.Clear();
    }
}
