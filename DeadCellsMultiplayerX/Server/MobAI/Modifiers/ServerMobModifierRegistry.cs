using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Modifiers.Zombie;
using DeadCellsMultiplayerX.Server.World.Ghosts;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Server.MobAI.Modifiers
{
    /// <summary>
    /// 服务端 mob 修改器注册器。
    /// 负责构造上下文、执行 InitHook、完成依赖注入并维护生命周期
    /// </summary>
    internal sealed class ServerMobModifierRegistry
    {
        private readonly ServerSession session;
        private readonly ServerGhostManager ghosts;
        private readonly ServerTeamManager teams;
        private readonly IServerMobEventBus events;
        private readonly MobModifierRegistry<ServerMobModifierContext> inner = new();

        public ServerMobModifierRegistry(
            ServerSession session,
            ServerGhostManager ghosts,
            ServerTeamManager teams,
            IServerMobEventBus events)
        {
            this.session = session;
            this.ghosts = ghosts;
            this.teams = teams;
            this.events = events;

            inner.Register<GameZombie>(
                ctx => new ZombieServerModifier(ctx.Session, ctx.Teams));
        }

        /// <summary>
        /// 注册一个服务端 mob 修改器工厂
        /// </summary>
        public void Register(
            System.Type mobType,
            Func<ServerMobModifierContext, IMobModifier> factory)
            => inner.Register(mobType, factory);

        /// <summary>
        /// 为服务端 mob 绑定修改器
        /// </summary>
        public bool TryAttach(Mob mob, long tick)
        {
            if (mob == null
                || !session.MobRegistry.TryGetNetId(mob, out uint netId))
                return false;

            var context = new ServerMobModifierContext(
                mob, netId, tick,
                session, ghosts, teams, events);

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

        public bool DispatchBehaviourAi(Mob mob, long tick)
            => inner.DispatchBehaviourAi(mob, tick);

        public bool DispatchFixedUpdateHandled(Mob mob, long tick)
            => inner.DispatchFixedUpdateHandled(mob, tick);

        public void DispatchPreUpdate(Mob mob, long tick)
            => inner.DispatchPreUpdate(mob, tick);

        public void DispatchFixedUpdate(Mob mob, long tick)
            => inner.DispatchFixedUpdate(mob, tick);

        public void DispatchPostUpdate(Mob mob, long tick)
            => inner.DispatchPostUpdate(mob, tick);

        public void DispatchSkillPrepare(Mob mob, OldSkill skill, long tick)
            => inner.DispatchSkillPrepare(mob, skill, tick);

        public void DispatchSkillExecute(Mob mob, OldSkill skill, long tick)
            => inner.DispatchSkillExecute(mob, skill, tick);

        public void DispatchMobDied(Mob mob, long tick)
            => inner.DispatchMobDied(mob, tick);

        public bool DispatchAttackResult(
            Mob mob, AttackData atk, Entity target, long tick)
            => inner.DispatchAttackResult(mob, atk, target, tick);

        public bool? DispatchAiLocked(Mob mob, long tick)
            => inner.DispatchAiLocked(mob, tick);

        public bool DispatchClientUpdate(Mob mob, long tick)
            => inner.DispatchClientUpdate(mob, tick);

        public void Tick(long tick) => inner.Tick(tick);

        public void Clear() => inner.Clear();
    }
}
