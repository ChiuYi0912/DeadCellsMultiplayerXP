using dc;
using dc.en;
using dc.pr;
using dc.hl.types;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;

using dc.cine;
using dc.tool.weap;
namespace DeadCellsMultiplayerX.Engine.Hooks.Client
{
    /// <summary>
    /// 客户端会话级 Hook 的端侧接收者。
    /// 覆盖存档、游戏生命周期、Mob、关卡、战斗与盾牌。
    /// </summary>
    internal interface IClientSessionHookSink
    {
        void OnSave(Hook__Save.orig_save orig, dc.User u, bool onlyGameData);

        dc.haxe.io.Bytes OnFileGetBytes(Hook__File.orig_getBytes orig, dc.String file);

        bool? OnGetSteamCloudStatus(HashlinkClosure orig);

        void OnSaveSteamCloudStatus(HashlinkClosure orig);

        void OnGameDispose(Hook_Game.orig_onDispose orig, dc.pr.Game self);

        void OnGameInit(Hook_Game.orig_init orig, dc.pr.Game self);

        bool OnMobAiLocked(Hook_Mob.orig_aiLocked orig, Mob self);

        void OnMobDie(Hook_Mob.orig_onDie orig, Mob self);

        bool OnMobCanUpdate(Hook_Mob.orig_canUpdate orig, Mob self);

        void OnMobFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self);

        bool OnMobMoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self);

        void OnMobInitSkills(Hook_Mob.orig_initSkills orig, Mob self);

        void OnLevelAttachMobs(Hook_Level.orig_attachMobs orig, Level self);

        void OnPowerUpdate(Hook_Power.orig_update orig, Power self);

        void OnApplyHitResult(
            Hook__AttackUtils.orig_applyHitResult orig,
            AttackData atk,
            Entity target);

        void OnPostHitResultUpdate(
            Hook__AttackUtils.orig_postHitResultUpdate orig,
            AttackData atk,
            Entity target);

        AttackData OnCreateFromHeroAndHit(
            Hook__AttackUtils.orig_createFromHeroAndHit orig,
            Entity parent,
            object baseDmg,
            int? data,
            Entity target);

        void OnAreaAffectInit(
            Hook_LevelAreaAffect.orig_init orig,
            dc.tool.LevelAreaAffect self,
            int x,
            int y,
            double durationS,
            int a,
            double aDurationS,
            double aValue,
            ArrayObj affixes);

        void OnAreaAffectApplyAnother(
            Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
            dc.tool.LevelAreaAffect self,
            double a,
            int aDurationS,
            double aValue,
            double affixes,
            ArrayObj prev);

        void OnOldMobSkillUpdate(Hook_OldMobSkill.orig_update orig, OldMobSkill self);

        void OnOldSkillInterrupt(Hook_OldSkill.orig_interrupt orig, OldSkill self);

        bool OnOldSkillIsReady(Hook_OldSkill.orig_isReady orig, OldSkill self);

        void OnCounterGrenade(
            Hook_BaseShield.orig_counterGrenade orig,
            BaseShield self,
            Grenade source);

        void OnLevelTransitionCtor(
            Hook__LevelTransition.orig___constructor__ orig,
            LevelTransition arg1,
            dc.String mainId,
            dc.level.LevelMap map,
            int? linkId,
            CPoint heroPosAfterBossRuneReload,
            Ref<bool> noLoadingData);
    }
}
