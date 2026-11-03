using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Modifiers.Zombie;
using DeadCellsMultiplayerX.Server.World.Ghosts;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
using DeadCellsMultiplayerX.Utils;
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

        /// <summary>
        /// 技能准备：注册过修改器的 mob 交给修改器处理；
        /// 其余 mob 统一收集并转发给客户端播放
        /// </summary>
        public void DispatchSkillPrepare(Mob mob, OldSkill skill, Entity target, int? data, long tick)
        {
            if (mob == null || mob.destroyed || skill == null || skill.destroyed) return;

            if (inner.TryGet(mob, out _))
            {
                if (!ghosts.TryGetCombatant(target, out var combatant))
                    return;

                inner.DispatchSkillPrepare(mob, skill, combatant, data, tick);
                return;
            }

            CollectSkillPrepare(mob, skill, target, data, tick);
        }

        /// <summary>
        /// 未注册修改器的 mob 统一收集技能准备事件
        /// </summary>
        private void CollectSkillPrepare(
            Mob mob, OldSkill skill, Entity target, int? data, long tick)
        {
            if (!session.MobRegistry.TryGetNetId(mob, out uint netId))
                return;

            string targetGuid = string.Empty;
            if (ghosts.TryGetCombatant(target, out var combatant))
                targetGuid = combatant.Guid;

            var level = mob._level;

            ghosts.MobAttacks.SubmitSkill(new MobModifierEvent
            {
                Epoch = session.Broadcaster.Epoch,
                MobNetId = netId,
                Kind = MobModifierEventKind.ActionStarted,
                SkillId = skill.id.ToString(),
                TargetGuid = targetGuid,
                SkillRatio = data,
                ServerTimeMs = tick,
                LevelId = level?.map?.id?.ToString() ?? string.Empty,
                SubLevelIndex = level?.GetSubLevelIndex() ?? -1,
            });
        }

        public void DispatchSkillExecute(Mob mob, OldSkill skill, long tick) => inner.DispatchSkillExecute(mob, skill, tick);

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
