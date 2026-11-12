namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc;
using dc.cine;
using dc.en;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using ModCore;

/// <summary>
/// 客户端会话级 Hook 的唯一订阅入口。
/// </summary>
internal static class ClientSessionHookBinding
{
    public static IDisposable Install(IClientSessionHookSink sink)
    {
        Hook__Save.save += sink.OnSave;
        Hook__File.getBytes += sink.OnFileGetBytes;
        Hook_Game.onDispose += sink.OnGameDispose;
        Hook_Game.init += sink.OnGameInit;
        Hook_Mob.aiLocked += sink.OnMobAiLocked;
        Hook_Mob.onDie += sink.OnMobDie;
        Hook_Mob.canUpdate += sink.OnMobCanUpdate;
        Hook_Mob.fixedUpdate += sink.OnMobFixedUpdate;
        Hook_Mob.moveBlocked += sink.OnMobMoveBlocked;
        Hook_Mob.initSkills += sink.OnMobInitSkills;
        Hook_Level.attachMobs += sink.OnLevelAttachMobs;
        Hook_Power.update += sink.OnPowerUpdate;
        Hook__AttackUtils.applyHitResult += sink.OnApplyHitResult;
        Hook__AttackUtils.postHitResultUpdate += sink.OnPostHitResultUpdate;
        Hook__AttackUtils.createFromHeroAndHit += sink.OnCreateFromHeroAndHit;
        Hook_LevelAreaAffect.init += sink.OnAreaAffectInit;
        Hook_LevelAreaAffect.applyAnotherAffect += sink.OnAreaAffectApplyAnother;
        Hook_OldMobSkill.update += sink.OnOldMobSkillUpdate;
        Hook_OldSkill.interrupt += sink.OnOldSkillInterrupt;
        Hook_OldSkill.isReady += sink.OnOldSkillIsReady;
        Hook_BaseShield.counterGrenade += sink.OnCounterGrenade;
        Hook__LevelTransition.__constructor__ += sink.OnLevelTransitionCtor;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook__Save.save -= sink.OnSave));
        group.Add(new HookLease(() => Hook__File.getBytes -= sink.OnFileGetBytes));
        group.Add(new HookLease(() => Hook_Game.onDispose -= sink.OnGameDispose));
        group.Add(new HookLease(() => Hook_Game.init -= sink.OnGameInit));
        group.Add(new HookLease(() => Hook_Mob.aiLocked -= sink.OnMobAiLocked));
        group.Add(new HookLease(() => Hook_Mob.onDie -= sink.OnMobDie));
        group.Add(new HookLease(() => Hook_Mob.canUpdate -= sink.OnMobCanUpdate));
        group.Add(new HookLease(() => Hook_Mob.fixedUpdate -= sink.OnMobFixedUpdate));
        group.Add(new HookLease(() => Hook_Mob.moveBlocked -= sink.OnMobMoveBlocked));
        group.Add(new HookLease(() => Hook_Mob.initSkills -= sink.OnMobInitSkills));
        group.Add(new HookLease(() => Hook_Level.attachMobs -= sink.OnLevelAttachMobs));
        group.Add(new HookLease(() => Hook_Power.update -= sink.OnPowerUpdate));
        group.Add(new HookLease(() => Hook__AttackUtils.applyHitResult -= sink.OnApplyHitResult));
        group.Add(new HookLease(() => Hook__AttackUtils.postHitResultUpdate -= sink.OnPostHitResultUpdate));
        group.Add(new HookLease(() => Hook__AttackUtils.createFromHeroAndHit -= sink.OnCreateFromHeroAndHit));
        group.Add(new HookLease(() => Hook_LevelAreaAffect.init -= sink.OnAreaAffectInit));
        group.Add(new HookLease(() => Hook_LevelAreaAffect.applyAnotherAffect -= sink.OnAreaAffectApplyAnother));
        group.Add(new HookLease(() => Hook_OldMobSkill.update -= sink.OnOldMobSkillUpdate));
        group.Add(new HookLease(() => Hook_OldSkill.interrupt -= sink.OnOldSkillInterrupt));
        group.Add(new HookLease(() => Hook_OldSkill.isReady -= sink.OnOldSkillIsReady));
        group.Add(new HookLease(() => Hook_BaseShield.counterGrenade -= sink.OnCounterGrenade));
        group.Add(new HookLease(() => Hook__LevelTransition.__constructor__ -= sink.OnLevelTransitionCtor));

        if (GameInfo.Platform == GameInfo.PlatformKind.Steam)
        {
            group.Add(new RawHookLease(ModCore.Modules.HashlinkHooks.Instance.CreateHook(
                "tool.$File", "getSteamCloudStatus", sink.OnGetSteamCloudStatus)));
            group.Add(new RawHookLease(ModCore.Modules.HashlinkHooks.Instance.CreateHook(
                "tool.$File", "saveSteamCloudStatus", sink.OnSaveSteamCloudStatus)));
        }

        return group;
    }
}
