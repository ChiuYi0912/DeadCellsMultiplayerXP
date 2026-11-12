using dc.pr;
using dc.en;
using dc.tool.skill;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// Mob 生命周期、AI 与技能 Hook 的唯一订阅入口。
    /// </summary>
    internal static class ServerMobHookBinding
    {
        public static IDisposable Install(IServerMobHookSink sink)
        {
            Hook_Level.attachMob += sink.OnAttach;
            Hook__Mob.create += sink.OnCreate;
            Hook_Mob.onDie += sink.OnDie;
            Hook_Mob.preUpdate += sink.OnPreUpdate;
            Hook_Mob.fixedUpdate += sink.OnFixedUpdate;
            Hook_Mob.postUpdate += sink.OnPostUpdate;
            Hook_Mob.onDamage += sink.OnDamage;
            Hook_Mob.onDirectHitFromHero += sink.OnDirectHitFromHero;
            Hook_Mob.updateThreat += sink.OnUpdateThreat;
            Hook_Mob.setNemesisTarget += sink.OnSetNemesisTarget;
            Hook_Mob.behaviourAi += sink.OnBehaviourAi;
            Hook_Mob.aiLocked += sink.OnAiLocked;
            Hook_OldSkill.prepare += sink.OnSkillPrepare;
            Hook_OldMobSkill.execute += sink.OnSkillExecute;

            var group = new HookGroup();
            group.Add(new HookLease(() => Hook_Level.attachMob -= sink.OnAttach));
            group.Add(new HookLease(() => Hook__Mob.create -= sink.OnCreate));
            group.Add(new HookLease(() => Hook_Mob.onDie -= sink.OnDie));
            group.Add(new HookLease(() => Hook_Mob.preUpdate -= sink.OnPreUpdate));
            group.Add(new HookLease(() => Hook_Mob.fixedUpdate -= sink.OnFixedUpdate));
            group.Add(new HookLease(() => Hook_Mob.postUpdate -= sink.OnPostUpdate));
            group.Add(new HookLease(() => Hook_Mob.onDamage -= sink.OnDamage));
            group.Add(new HookLease(() => Hook_Mob.onDirectHitFromHero -= sink.OnDirectHitFromHero));
            group.Add(new HookLease(() => Hook_Mob.updateThreat -= sink.OnUpdateThreat));
            group.Add(new HookLease(() => Hook_Mob.setNemesisTarget -= sink.OnSetNemesisTarget));
            group.Add(new HookLease(() => Hook_Mob.behaviourAi -= sink.OnBehaviourAi));
            group.Add(new HookLease(() => Hook_Mob.aiLocked -= sink.OnAiLocked));
            group.Add(new HookLease(() => Hook_OldSkill.prepare -= sink.OnSkillPrepare));
            group.Add(new HookLease(() => Hook_OldMobSkill.execute -= sink.OnSkillExecute));
            return group;
        }
    }
}
