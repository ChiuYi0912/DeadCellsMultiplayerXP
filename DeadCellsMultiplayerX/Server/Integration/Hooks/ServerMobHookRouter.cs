using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.Integration.Events;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior;
using HaxeProxy.Runtime;
using ModCore.Events;

using dc.pr;
namespace DeadCellsMultiplayerX.Server.Integration.Hooks
{
    /// <summary>
    /// Mob Hook 路由：只把原版 Hook 参数适配到 <see cref="ServerMobRuntime"/>。
    /// 创建/挂载、AI、伤害、死亡与技能的实际处理在 Server/World/Mobs 下。
    /// </summary>
    internal sealed class ServerMobHookRouter : IServerMobHookSink
    {
        private readonly ServerMain server;

        public ServerMobHookRouter(ServerMain server)
        {
            this.server = server;
        }

        public Mob OnCreate(
            Hook__Mob.orig_create orig,
            dc.String k,
            Level level,
            int cx,
            int cy,
            int dmgTier,
            Ref<int> lifeTier)
        {
            var mob = orig(k, level, cx, cy, dmgTier, lifeTier);
            var session = server.Session;

            if (session != null)
            {
                session.Entities.Register(mob, null, level);
                session.Ghosts.Mobs.TryAttach(mob);
            }

            int? safelifeTier = lifeTier.IsNull ? 0 : lifeTier.value;
            EventSystem.BroadcastEvent<IOnCreateMob, IOnCreateMob.Data>(
                new(mob, k, cx, cy, dmgTier, safelifeTier));
            return mob;
        }

        public Mob OnAttach(
            Hook_Level.orig_attachMob orig,
            Level self,
            dc.level.Mob m)
        {
            var mob = orig(self, m);
            var session = server.Session;

            if (session != null)
            {
                session.Entities.Register(mob, m, self);
                session.Ghosts.Mobs.TryAttach(mob);
            }

            EventSystem.BroadcastEvent<IOnAttachMob, IOnAttachMob.Data>(new(m, mob));

            return mob;
        }

        public void OnDie(Hook_Mob.orig_onDie orig, Mob self)
        {
            var session = server.Session;
            var runtime = session?.Ghosts.Mobs;

            if (runtime != null)
                runtime.OnMobDied(self, () => orig(self));
            else
                orig(self);

            session?.Entities.MarkDead(self);
        }

        public void OnPreUpdate(Hook_Mob.orig_preUpdate orig, Mob self)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null)
                runtime.OnPreUpdate(self, () => orig(self));
            else
                orig(self);
        }

        public void OnFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime == null)
            {
                orig(self);
                return;
            }

            runtime.OnFixedUpdate(self, () => orig(self));
        }

        public void OnPostUpdate(Hook_Mob.orig_postUpdate orig, Mob self)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null)
                runtime.OnPostUpdate(self, () => orig(self));
            else
                orig(self);
        }

        public void OnDamage(Hook_Mob.orig_onDamage orig, Mob self, AttackData atk)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null)
                runtime.OnDamage(self, atk, () => orig(self, atk));
            else
                orig(self, atk);
        }

        public void OnDirectHitFromHero(
            Hook_Mob.orig_onDirectHitFromHero orig,
            Mob self,
            AttackData atk)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null)
                runtime.OnDirectHitFromHero(self, atk, () => orig(self, atk));
            else
                orig(self, atk);
        }

        /// <summary>
        /// 原版 updateThreat 之后只做联机目标修复。
        /// </summary>
        public void OnUpdateThreat(Hook_Mob.orig_updateThreat orig, Mob self)
        {
            orig(self);
            server.Session?.Ghosts.Mobs.OnThreatUpdated(self);
        }

        /// <summary>
        /// 阻止 mob 硬锁定服务端假人，改选最近的 Ghost。
        /// </summary>
        public void OnSetNemesisTarget(
            Hook_Mob.orig_setNemesisTarget orig,
            Mob self,
            Entity target)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null
                && runtime.TryRedirectNemesis(self, target, out var redirected))
            {
                if (redirected == null)
                {
                    self.clearNemesisTarget();
                    return;
                }

                target = redirected;
            }

            orig(self, target);
        }

        /// <summary>
        /// 交给已注册的 mob 修改器接管原版 AI；未接管时继续原版逻辑。
        /// </summary>
        public void OnBehaviourAi(Hook_Mob.orig_behaviourAi orig, Mob self)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime != null && runtime.OnBehaviourAi(self))
                return;

            orig(self);
        }

        public bool OnAiLocked(Hook_Mob.orig_aiLocked orig, Mob self)
        {
            var runtime = server.Session?.Ghosts.Mobs;

            if (runtime == null)
                return orig(self);

            return runtime.OnAiLocked(self, () => orig(self));
        }

        /// <summary>
        /// 通知 mob 修改器技能已准备。
        /// </summary>
        public bool OnSkillPrepare(
            Hook_OldSkill.orig_prepare orig,
            OldSkill self,
            int? data)
        {
            bool prepared = orig(self, data);

            if (prepared)
                server.Session?.Ghosts.Mobs.OnSkillPrepare(self, data);

            return prepared;
        }

        /// <summary>
        /// 通知 mob 修改器技能已执行。
        /// </summary>
        public void OnSkillExecute(
            Hook_OldMobSkill.orig_execute orig,
            OldMobSkill self,
            double? ratio)
        {
            server.Session?.Ghosts.Mobs.OnSkillExecute(self);

            orig(self, ratio);
        }
    }
}
