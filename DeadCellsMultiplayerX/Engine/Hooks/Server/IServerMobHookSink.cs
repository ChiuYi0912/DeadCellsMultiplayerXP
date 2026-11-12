using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using HaxeProxy.Runtime;

using dc.pr;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// Mob 生命周期、AI 与技能 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerMobHookSink
    {
        Mob OnCreate(
            Hook__Mob.orig_create orig,
            dc.String k,
            Level level,
            int cx,
            int cy,
            int dmgTier,
            Ref<int> lifeTier);

        Mob OnAttach(
            Hook_Level.orig_attachMob orig,
            Level self,
            dc.level.Mob m);

        void OnDie(Hook_Mob.orig_onDie orig, Mob self);

        void OnPreUpdate(Hook_Mob.orig_preUpdate orig, Mob self);

        void OnFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self);

        void OnPostUpdate(Hook_Mob.orig_postUpdate orig, Mob self);

        void OnDamage(Hook_Mob.orig_onDamage orig, Mob self, AttackData atk);

        void OnDirectHitFromHero(
            Hook_Mob.orig_onDirectHitFromHero orig,
            Mob self,
            AttackData atk);

        void OnUpdateThreat(Hook_Mob.orig_updateThreat orig, Mob self);

        void OnSetNemesisTarget(
            Hook_Mob.orig_setNemesisTarget orig,
            Mob self,
            Entity target);

        void OnBehaviourAi(Hook_Mob.orig_behaviourAi orig, Mob self);

        bool OnAiLocked(Hook_Mob.orig_aiLocked orig, Mob self);

        bool OnSkillPrepare(
            Hook_OldSkill.orig_prepare orig,
            OldSkill self,
            int? data);

        void OnSkillExecute(
            Hook_OldMobSkill.orig_execute orig,
            OldMobSkill self,
            double? ratio);
    }
}
