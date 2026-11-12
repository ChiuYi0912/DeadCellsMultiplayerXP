using dc;
using dc.en;
using dc.tool;
using dc.tool._Cooldown;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Server.World.Actors;

namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior
{
    /// <summary>
    /// 服务端 mob 运行时：把 Hook 的 Mob 生命周期、AI、伤害与技能事件
    /// 收敛为单一入口，Hook 路由只负责适配原版签名。
    /// </summary>
    internal sealed class ServerMobRuntime
    {
        private const int AI_LOCK_ID = 54525952;
        private const double INFINITE_FRAMES = double.MaxValue;

        private readonly ServerMobModifierRegistry modifiers;
        private readonly ServerAggroManager aggro;
        private readonly Func<long> clock;

        public ServerMobRuntime(
            ServerMobModifierRegistry modifiers,
            ServerAggroManager aggro,
            Func<long> clock)
        {
            this.modifiers = modifiers
                ?? throw new ArgumentNullException(nameof(modifiers));
            this.aggro = aggro
                ?? throw new ArgumentNullException(nameof(aggro));
            this.clock = clock
                ?? throw new ArgumentNullException(nameof(clock));
        }

        public void TryAttach(Mob mob) => modifiers.TryAttach(mob, clock());

        public void Detach(Mob mob) => modifiers.Detach(mob);

        public void Tick(long nowMs) => modifiers.Tick(nowMs);

        public void Clear() => modifiers.Clear();

        /// <summary>
        /// mob 死亡：先通知修改器，再在对应 Ghost 的 Hero 上下文里执行原版死亡。
        /// </summary>
        public void OnMobDied(Mob mob, Action original)
        {
            modifiers.DispatchMobDied(mob, clock());
            aggro.RunAsMobHero(mob, original);
        }

        public void OnPreUpdate(Mob mob, Action original)
        {
            modifiers.DispatchPreUpdate(mob, clock());
            aggro.RunAsMobHero(mob, original);
        }

        /// <summary>
        /// 返回 true 表示修改器已接管本帧，调用方不得再执行原版逻辑。
        /// </summary>
        public bool OnFixedUpdate(Mob mob, Action original)
        {
            long tick = clock();
            if (modifiers.DispatchFixedUpdateHandled(mob, tick))
                return true;

            modifiers.DispatchFixedUpdate(mob, tick);
            aggro.RunAsMobHero(mob, original);
            return false;
        }

        public void OnPostUpdate(Mob mob, Action original)
        {
            aggro.RunAsMobHero(mob, original);
            modifiers.DispatchPostUpdate(mob, clock());
        }

        public void OnDamage(Mob mob, AttackData atk, Action original)
        {
            var hero = aggro.ResolveAttackerHero(atk)
                ?? aggro.ResolveMobHero(mob);
            aggro.RunAsHero(hero, original);
        }

        public void OnDirectHitFromHero(Mob mob, AttackData atk, Action original)
        {
            OnDamage(mob, atk, original);
        }

        public void OnThreatUpdated(Mob mob)
        {
            aggro.OnThreatUpdated(mob);
        }

        /// <summary>
        /// 阻止 mob 硬锁定服务端假人；返回 true 表示调用方需要改用 <paramref name="target"/>。
        /// </summary>
        public bool TryRedirectNemesis(
            Mob mob, Entity? requested, out Entity? target)
        {
            return aggro.TryRedirectNemesis(mob, requested, out target);
        }

        /// <summary>
        /// 返回 true 表示修改器已接管 AI 行为。
        /// </summary>
        public bool OnBehaviourAi(Mob mob)
        {
            return modifiers.DispatchBehaviourAi(mob, clock());
        }

        /// <summary>
        /// 是否把原版 AI 锁定；修改器要求锁定时写入无限冷却。
        /// </summary>
        public bool OnAiLocked(Mob mob, Func<bool> original)
        {
            bool? handled = modifiers.DispatchAiLocked(mob, clock());

            if (handled == null || handled == false)
                return original();

            Cooldown cd = mob.cd;
            CdInst inst = cd.fastCheck.get(AI_LOCK_ID);

            if (inst == null)
            {
                inst = new CdInst(AI_LOCK_ID, INFINITE_FRAMES);
                cd.fastCheck.set(AI_LOCK_ID, inst);
                cd.cdList.push(inst);
            }
            else
            {
                inst.frames = INFINITE_FRAMES;
            }

            return true;
        }

        /// <summary>
        /// 技能准备成功后通知修改器。
        /// </summary>
        public void OnSkillPrepare(OldSkill skill, int? data)
        {
            if (skill?.owner is not Mob mob)
                return;

            modifiers.DispatchSkillPrepare(
                mob, skill, mob.aTarget, data, clock());
        }

        /// <summary>
        /// 技能执行时通知修改器。
        /// </summary>
        public void OnSkillExecute(OldMobSkill skill)
        {
            if (skill?.owner is not Mob mob)
                return;

            modifiers.DispatchSkillExecute(mob, skill, clock());
        }
    }
}
