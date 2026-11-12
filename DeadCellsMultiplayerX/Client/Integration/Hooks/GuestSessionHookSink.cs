using dc;
using dc.cine;
using dc.en;
using dc.hl.types;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;

namespace DeadCellsMultiplayerX.Client.Integration.Hooks
{
    /// <summary>
    /// 会话 Hook 的分发器：把 <see cref="IClientSessionHookSink"/> 的宽接口
    /// 按战斗、Mob、存档、游戏生命周期四个子域交给独立适配器。
    /// 会话本身不再实现 Hook 逻辑。
    /// </summary>
    internal sealed class GuestSessionHookSink : IClientSessionHookSink
    {
        private readonly GuestCombatHookAdapter combat;
        private readonly GuestMobHookAdapter mob;
        private readonly GuestSaveHookAdapter save;
        private readonly GuestGameHookAdapter game;

        public GuestSessionHookSink(
            GuestCombatHookAdapter combat,
            GuestMobHookAdapter mob,
            GuestSaveHookAdapter save,
            GuestGameHookAdapter game)
        {
            this.combat = combat;
            this.mob = mob;
            this.save = save;
            this.game = game;
        }

        public void OnSave(Hook__Save.orig_save orig, dc.User u, bool onlyGameData)
            => save.Save(orig, u, onlyGameData);

        public dc.haxe.io.Bytes OnFileGetBytes(
            Hook__File.orig_getBytes orig,
            dc.String file)
            => save.FileGetBytes(orig, file);

        public bool? OnGetSteamCloudStatus(HashlinkClosure orig)
            => save.GetSteamCloudStatus(orig);

        public void OnSaveSteamCloudStatus(HashlinkClosure orig)
            => save.SaveSteamCloudStatus(orig);

        public void OnGameDispose(Hook_Game.orig_onDispose orig, dc.pr.Game self)
            => game.GameDispose(orig, self);

        public void OnGameInit(Hook_Game.orig_init orig, dc.pr.Game self)
            => game.GameInit(orig, self);

        public bool OnMobAiLocked(Hook_Mob.orig_aiLocked orig, Mob self)
            => mob.AiLocked(orig, self);

        public void OnMobDie(Hook_Mob.orig_onDie orig, Mob self)
            => mob.OnDie(orig, self);

        public bool OnMobCanUpdate(Hook_Mob.orig_canUpdate orig, Mob self)
            => mob.CanUpdate(orig, self);

        public void OnMobFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
            => mob.FixedUpdate(orig, self);

        public bool OnMobMoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self)
            => mob.MoveBlocked(orig, self);

        public void OnMobInitSkills(Hook_Mob.orig_initSkills orig, Mob self)
            => mob.InitSkills(orig, self);

        public void OnLevelAttachMobs(Hook_Level.orig_attachMobs orig, Level self)
            => mob.AttachMobs(orig, self);

        public void OnPowerUpdate(Hook_Power.orig_update orig, Power self)
            => combat.PowerUpdate(orig, self);

        public void OnApplyHitResult(
            Hook__AttackUtils.orig_applyHitResult orig,
            AttackData atk,
            Entity target)
            => combat.ApplyHitResult(orig, atk, target);

        public void OnPostHitResultUpdate(
            Hook__AttackUtils.orig_postHitResultUpdate orig,
            AttackData atk,
            Entity target)
            => combat.PostHitResultUpdate(orig, atk, target);

        public AttackData OnCreateFromHeroAndHit(
            Hook__AttackUtils.orig_createFromHeroAndHit orig,
            Entity parent,
            object baseDmg,
            int? data,
            Entity target)
            => combat.CreateFromHeroAndHit(orig, parent, baseDmg, data, target);

        public void OnAreaAffectInit(
            Hook_LevelAreaAffect.orig_init orig,
            dc.tool.LevelAreaAffect self,
            int x,
            int y,
            double durationS,
            int a,
            double aDurationS,
            double aValue,
            ArrayObj affixes)
            => combat.AreaAffectInit(orig, self, x, y, durationS, a, aDurationS, aValue, affixes);

        public void OnAreaAffectApplyAnother(
            Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
            dc.tool.LevelAreaAffect self,
            double a,
            int aDurationS,
            double aValue,
            double affixes,
            ArrayObj prev)
            => combat.AreaAffectApplyAnother(orig, self, a, aDurationS, aValue, affixes, prev);

        public void OnOldMobSkillUpdate(
            Hook_OldMobSkill.orig_update orig,
            OldMobSkill self)
            => mob.OldMobSkillUpdate(orig, self);

        public void OnOldSkillInterrupt(
            Hook_OldSkill.orig_interrupt orig,
            OldSkill self)
            => mob.OldSkillInterrupt(orig, self);

        public bool OnOldSkillIsReady(
            Hook_OldSkill.orig_isReady orig,
            OldSkill self)
            => mob.OldSkillIsReady(orig, self);

        public void OnCounterGrenade(
            Hook_BaseShield.orig_counterGrenade orig,
            BaseShield self,
            Grenade source)
            => combat.CounterGrenade(orig, self, source);

        public void OnLevelTransitionCtor(
            Hook__LevelTransition.orig___constructor__ orig,
            LevelTransition arg1,
            dc.String mainId,
            dc.level.LevelMap map,
            int? linkId,
            CPoint heroPosAfterBossRuneReload,
            Ref<bool> noLoadingData)
            => game.LevelTransitionCtor(
                orig, arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
    }
}
