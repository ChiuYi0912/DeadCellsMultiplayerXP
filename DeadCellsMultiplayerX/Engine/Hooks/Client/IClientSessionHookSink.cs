namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc;
using dc.cine;
using dc.en;
using dc.hl.types;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using dc.tool.weap;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;

/// <summary>
/// 客户端会话级 Hook 的端侧接收者。
/// 覆盖存档、游戏生命周期、Mob、关卡、战斗与盾牌。
/// </summary>
internal interface IClientSessionHookSink
{
    public void OnSave(Hook__Save.orig_save orig, User u, bool onlyGameData);

    public dc.haxe.io.Bytes OnFileGetBytes(Hook__File.orig_getBytes orig, String file);

    public bool? OnGetSteamCloudStatus(HashlinkClosure orig);

    public void OnSaveSteamCloudStatus(HashlinkClosure orig);

    public void OnGameDispose(Hook_Game.orig_onDispose orig, Game self);

    public void OnGameInit(Hook_Game.orig_init orig, Game self);

    public bool OnMobAiLocked(Hook_Mob.orig_aiLocked orig, Mob self);

    public void OnMobDie(Hook_Mob.orig_onDie orig, Mob self);

    public bool OnMobCanUpdate(Hook_Mob.orig_canUpdate orig, Mob self);

    public void OnMobFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self);

    public bool OnMobMoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self);

    public void OnMobInitSkills(Hook_Mob.orig_initSkills orig, Mob self);

    public void OnLevelAttachMobs(Hook_Level.orig_attachMobs orig, Level self);

    public void OnPowerUpdate(Hook_Power.orig_update orig, Power self);

    public void OnApplyHitResult(
        Hook__AttackUtils.orig_applyHitResult orig,
        AttackData atk,
        Entity target);

    public void OnPostHitResultUpdate(
        Hook__AttackUtils.orig_postHitResultUpdate orig,
        AttackData atk,
        Entity target);

    public AttackData OnCreateFromHeroAndHit(
        Hook__AttackUtils.orig_createFromHeroAndHit orig,
        Entity parent,
        object baseDmg,
        int? data,
        Entity target);

    public void OnAreaAffectInit(
        Hook_LevelAreaAffect.orig_init orig,
        LevelAreaAffect self,
        int x,
        int y,
        double durationS,
        int a,
        double aDurationS,
        double aValue,
        ArrayObj affixes);

    public void OnAreaAffectApplyAnother(
        Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
        LevelAreaAffect self,
        double a,
        int aDurationS,
        double aValue,
        double affixes,
        ArrayObj prev);

    public void OnOldMobSkillUpdate(Hook_OldMobSkill.orig_update orig, OldMobSkill self);

    public void OnOldSkillInterrupt(Hook_OldSkill.orig_interrupt orig, OldSkill self);

    public bool OnOldSkillIsReady(Hook_OldSkill.orig_isReady orig, OldSkill self);

    public void OnCounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source);

    public void OnLevelTransitionCtor(
        Hook__LevelTransition.orig___constructor__ orig,
        LevelTransition arg1,
        String mainId,
        dc.level.LevelMap map,
        int? linkId,
        CPoint heroPosAfterBossRuneReload,
        Ref<bool> noLoadingData);
}
