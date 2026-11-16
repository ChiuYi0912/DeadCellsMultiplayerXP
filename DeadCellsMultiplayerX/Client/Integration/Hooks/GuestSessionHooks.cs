namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

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

/// <summary>Installs the guest's save, game, mob and combat hooks as one owned lease.</summary>
internal sealed class GuestSessionHooks : IDisposable
{
    private readonly HookGroup group = new();

    public GuestSessionHooks(GuestCombatHookAdapter combat, GuestMobHookAdapter mob,
        GuestSaveHookAdapter save, GuestGameHookAdapter game)
    {
        this.group.Subscribe(
            () => Hook__Save.save += GuestSaveHookAdapter.Save,
            () => Hook__Save.save -= GuestSaveHookAdapter.Save);

        this.group.Subscribe(
            () => Hook__File.getBytes += save.FileGetBytes,
            () => Hook__File.getBytes -= save.FileGetBytes);

        this.group.Subscribe(
            () => Hook_Game.onDispose += game.GameDispose,
            () => Hook_Game.onDispose -= game.GameDispose);

        this.group.Subscribe(
            () => Hook_Game.init += game.GameInit,
            () => Hook_Game.init -= game.GameInit);

        this.group.Subscribe(
            () => Hook_Mob.aiLocked += mob.AiLocked,
            () => Hook_Mob.aiLocked -= mob.AiLocked);

        this.group.Subscribe(
            () => Hook_Mob.onDie += mob.OnDie,
            () => Hook_Mob.onDie -= mob.OnDie);

        this.group.Subscribe(
            () => Hook_Mob.canUpdate += GuestMobHookAdapter.CanUpdate,
            () => Hook_Mob.canUpdate -= GuestMobHookAdapter.CanUpdate);

        this.group.Subscribe(
            () => Hook_Mob.fixedUpdate += mob.FixedUpdate,
            () => Hook_Mob.fixedUpdate -= mob.FixedUpdate);

        this.group.Subscribe(
            () => Hook_Mob.moveBlocked += GuestMobHookAdapter.MoveBlocked,
            () => Hook_Mob.moveBlocked -= GuestMobHookAdapter.MoveBlocked);

        this.group.Subscribe(
            () => Hook_Mob.initSkills += mob.InitSkills,
            () => Hook_Mob.initSkills -= mob.InitSkills);

        this.group.Subscribe(
            () => Hook_Level.attachMobs += GuestMobHookAdapter.AttachMobs,
            () => Hook_Level.attachMobs -= GuestMobHookAdapter.AttachMobs);

        this.group.Subscribe(
            () => Hook_Power.update += GuestCombatHookAdapter.PowerUpdate,
            () => Hook_Power.update -= GuestCombatHookAdapter.PowerUpdate);

        this.group.Subscribe(
            () => Hook__AttackUtils.applyHitResult += combat.ApplyHitResult,
            () => Hook__AttackUtils.applyHitResult -= combat.ApplyHitResult);

        this.group.Subscribe(
            () => Hook__AttackUtils.postHitResultUpdate += GuestCombatHookAdapter.PostHitResultUpdate,
            () => Hook__AttackUtils.postHitResultUpdate -= GuestCombatHookAdapter.PostHitResultUpdate);

        this.group.Subscribe(
            () => Hook__AttackUtils.createFromHeroAndHit += combat.CreateFromHeroAndHit,
            () => Hook__AttackUtils.createFromHeroAndHit -= combat.CreateFromHeroAndHit);

        this.group.Subscribe(
            () => Hook_LevelAreaAffect.init += combat.AreaAffectInit,
            () => Hook_LevelAreaAffect.init -= combat.AreaAffectInit);

        this.group.Subscribe(
            () => Hook_LevelAreaAffect.applyAnotherAffect += combat.AreaAffectApplyAnother,
            () => Hook_LevelAreaAffect.applyAnotherAffect -= combat.AreaAffectApplyAnother);

        this.group.Subscribe(
            () => Hook_OldMobSkill.update += GuestMobHookAdapter.OldMobSkillUpdate,
            () => Hook_OldMobSkill.update -= GuestMobHookAdapter.OldMobSkillUpdate);

        this.group.Subscribe(
            () => Hook_OldSkill.interrupt += GuestMobHookAdapter.OldSkillInterrupt,
            () => Hook_OldSkill.interrupt -= GuestMobHookAdapter.OldSkillInterrupt);

        this.group.Subscribe(
            () => Hook_OldSkill.isReady += GuestMobHookAdapter.OldSkillIsReady,
            () => Hook_OldSkill.isReady -= GuestMobHookAdapter.OldSkillIsReady);

        this.group.Subscribe(
            () => Hook_BaseShield.counterGrenade += GuestCombatHookAdapter.CounterGrenade,
            () => Hook_BaseShield.counterGrenade -= GuestCombatHookAdapter.CounterGrenade);

        this.group.Subscribe(
            () => Hook__LevelTransition.__constructor__ += game.LevelTransitionCtor,
            () => Hook__LevelTransition.__constructor__ -= game.LevelTransitionCtor);

        if (GameInfo.Platform == GameInfo.PlatformKind.Steam)
        {
            this.group.Add(new RawHookLease(
                ModCore.Modules.HashlinkHooks.Instance.CreateHook(
                    "tool.$File",
                    "getSteamCloudStatus",
                    GuestSaveHookAdapter.GetSteamCloudStatus)));

            this.group.Add(new RawHookLease(
                ModCore.Modules.HashlinkHooks.Instance.CreateHook(
                    "tool.$File",
                    "saveSteamCloudStatus",
                    GuestSaveHookAdapter.SaveSteamCloudStatus)));
        }
    }

    public void Dispose() => this.group.Dispose();
}
