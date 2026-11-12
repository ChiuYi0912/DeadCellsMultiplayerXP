namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
using dc.en;
using dc.pr;
using dc.tool.atk;
using dc.tool.skill;
using HaxeProxy.Runtime;

/// <summary>
/// Mob 生命周期、AI 与技能 Hook 的端侧接收者。
/// </summary>
internal interface IServerMobHookSink
{
    public Mob OnCreate(
        Hook__Mob.orig_create orig,
        String k,
        Level level,
        int cx,
        int cy,
        int dmgTier,
        Ref<int> lifeTier);

    public Mob OnAttach(
        Hook_Level.orig_attachMob orig,
        Level self,
        dc.level.Mob m);

    public void OnDie(Hook_Mob.orig_onDie orig, Mob self);

    public void OnPreUpdate(Hook_Mob.orig_preUpdate orig, Mob self);

    public void OnFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self);

    public void OnPostUpdate(Hook_Mob.orig_postUpdate orig, Mob self);

    public void OnDamage(Hook_Mob.orig_onDamage orig, Mob self, AttackData atk);

    public void OnDirectHitFromHero(
        Hook_Mob.orig_onDirectHitFromHero orig,
        Mob self,
        AttackData atk);

    public void OnUpdateThreat(Hook_Mob.orig_updateThreat orig, Mob self);

    public void OnSetNemesisTarget(
        Hook_Mob.orig_setNemesisTarget orig,
        Mob self,
        Entity target);

    public void OnBehaviourAi(Hook_Mob.orig_behaviourAi orig, Mob self);

    public bool OnAiLocked(Hook_Mob.orig_aiLocked orig, Mob self);

    public bool OnSkillPrepare(
        Hook_OldSkill.orig_prepare orig,
        OldSkill self,
        int? data);

    public void OnSkillExecute(
        Hook_OldMobSkill.orig_execute orig,
        OldMobSkill self,
        double? ratio);
}
