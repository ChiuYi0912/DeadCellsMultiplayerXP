namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

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

/// <summary>
/// 会话 Hook 的分发器：把 <see cref="IClientSessionHookSink"/> 的宽接口
/// 按战斗、Mob、存档、游戏生命周期四个子域交给独立适配器。
/// 会话本身不再实现 Hook 逻辑。
/// </summary>
internal sealed class GuestSessionHookSink(
    GuestCombatHookAdapter combat,
    GuestMobHookAdapter mob,
    GuestSaveHookAdapter save,
    GuestGameHookAdapter game) : IClientSessionHookSink
{
    private readonly GuestCombatHookAdapter combat = combat;
    private readonly GuestMobHookAdapter mob = mob;
    private readonly GuestSaveHookAdapter save = save;
    private readonly GuestGameHookAdapter game = game;

    public void OnSave(Hook__Save.orig_save orig, User u, bool onlyGameData)
        => GuestSaveHookAdapter.Save(orig, u, onlyGameData);

    public dc.haxe.io.Bytes OnFileGetBytes(
        Hook__File.orig_getBytes orig,
        String file)
        => this.save.FileGetBytes(orig, file);

    public bool? OnGetSteamCloudStatus(HashlinkClosure orig)
        => GuestSaveHookAdapter.GetSteamCloudStatus(orig);

    public void OnSaveSteamCloudStatus(HashlinkClosure orig)
        => GuestSaveHookAdapter.SaveSteamCloudStatus(orig);

    public void OnGameDispose(Hook_Game.orig_onDispose orig, Game self)
        => this.game.GameDispose(orig, self);

    public void OnGameInit(Hook_Game.orig_init orig, Game self)
        => this.game.GameInit(orig, self);

    public bool OnMobAiLocked(Hook_Mob.orig_aiLocked orig, Mob self)
        => this.mob.AiLocked(orig, self);

    public void OnMobDie(Hook_Mob.orig_onDie orig, Mob self)
        => this.mob.OnDie(orig, self);

    public bool OnMobCanUpdate(Hook_Mob.orig_canUpdate orig, Mob self)
        => GuestMobHookAdapter.CanUpdate(orig, self);

    public void OnMobFixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
        => this.mob.FixedUpdate(orig, self);

    public bool OnMobMoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self)
        => GuestMobHookAdapter.MoveBlocked(orig, self);

    public void OnMobInitSkills(Hook_Mob.orig_initSkills orig, Mob self)
        => this.mob.InitSkills(orig, self);

    public void OnLevelAttachMobs(Hook_Level.orig_attachMobs orig, Level self)
        => GuestMobHookAdapter.AttachMobs(orig, self);

    public void OnPowerUpdate(Hook_Power.orig_update orig, Power self)
        => GuestCombatHookAdapter.PowerUpdate(orig, self);

    public void OnApplyHitResult(
        Hook__AttackUtils.orig_applyHitResult orig,
        AttackData atk,
        Entity target)
        => this.combat.ApplyHitResult(orig, atk, target);

    public void OnPostHitResultUpdate(
        Hook__AttackUtils.orig_postHitResultUpdate orig,
        AttackData atk,
        Entity target)
        => GuestCombatHookAdapter.PostHitResultUpdate(orig, atk, target);

    public AttackData OnCreateFromHeroAndHit(
        Hook__AttackUtils.orig_createFromHeroAndHit orig,
        Entity parent,
        object baseDmg,
        int? data,
        Entity target)
        => this.combat.CreateFromHeroAndHit(orig, parent, baseDmg, data, target);

    public void OnAreaAffectInit(
        Hook_LevelAreaAffect.orig_init orig,
        LevelAreaAffect self,
        int x,
        int y,
        double durationS,
        int a,
        double aDurationS,
        double aValue,
        ArrayObj affixes)
        => this.combat.AreaAffectInit(orig, self, x, y, durationS, a, aDurationS, aValue, affixes);

    public void OnAreaAffectApplyAnother(
        Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
        LevelAreaAffect self,
        double a,
        int aDurationS,
        double aValue,
        double affixes,
        ArrayObj prev)
        => this.combat.AreaAffectApplyAnother(orig, self, a, aDurationS, aValue, affixes, prev);

    public void OnOldMobSkillUpdate(
        Hook_OldMobSkill.orig_update orig,
        OldMobSkill self)
        => GuestMobHookAdapter.OldMobSkillUpdate(orig, self);

    public void OnOldSkillInterrupt(
        Hook_OldSkill.orig_interrupt orig,
        OldSkill self)
        => GuestMobHookAdapter.OldSkillInterrupt(orig, self);

    public bool OnOldSkillIsReady(
        Hook_OldSkill.orig_isReady orig,
        OldSkill self)
        => GuestMobHookAdapter.OldSkillIsReady(orig, self);

    public void OnCounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source)
        => GuestCombatHookAdapter.CounterGrenade(orig, self, source);

    public void OnLevelTransitionCtor(
        Hook__LevelTransition.orig___constructor__ orig,
        LevelTransition arg1,
        String mainId,
        dc.level.LevelMap map,
        int? linkId,
        CPoint heroPosAfterBossRuneReload,
        Ref<bool> noLoadingData)
        => this.game.LevelTransitionCtor(
            orig, arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
}
