namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using dc.en;
using dc.tool.atk;
using dc.tool.hero.activeSkills;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
using DeadCellsMultiplayerX.Simulation.Loadout;
using Serilog;

/// <summary>
/// Shared Hero loadout/weapon/talisman operations.  The client and server
/// differ only in the skill endpoint; that endpoint lives in the role runtime
/// below instead of being a co-located Cast/Replay pair.
/// </summary>
internal abstract class HeroActionRuntime : IDisposable
{
    private readonly GhostTalismans talismans;
    private bool disposed;

    protected HeroActionRuntime(Hero hero, string ownerGuid)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);

        this.Context = new GhostActionContext(hero, ownerGuid);
        this.Skills = new GhostSkills(this.Context);
        this.Weapons = new GhostWeapons(this.Context);
        this.talismans = new GhostTalismans(this.Context);
    }

    protected GhostActionContext Context { get; }
    protected GhostSkills Skills { get; }
    protected GhostWeapons Weapons { get; }

    public bool ApplyLoadout(PlayerLoadout? loadout)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (loadout == null)
        {
            return false;
        }

        var ok = true;

        for (var i = 0; i < loadout.Weapons.Count && i < 2; i++)
        {
            var item = LoadoutCodec.ToItemRef(loadout.Weapons[i], i);
            if (item == null)
            {
                continue;
            }

            ok &= this.Weapons.Prepare(i, item);
        }

        for (var i = 0; i < loadout.Actives.Count && i < 2; i++)
        {
            var item = LoadoutCodec.ToItemRef(loadout.Actives[i], i);
            if (item == null)
            {
                continue;
            }

            ok &= this.Skills.Prepare(i, item);
        }

        // null 表示清空项链
        ok &= this.talismans.Apply(
            LoadoutCodec.ToItemRef(loadout.Talisman, 0));

        return ok;
    }

    public bool PrepareSkill(int slot, ItemRef? snapshot)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.Skills.Prepare(slot, snapshot);
    }

    public bool PrepareWeapon(int slot, ItemRef? snapshot)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.Weapons.Prepare(slot, snapshot);
    }

    public bool UseWeapon(int slot, int cycle)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.Weapons.Use(slot, cycle);
    }

    public bool ApplyTalisman(ItemRef? snapshot)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        return this.talismans.Apply(snapshot);
    }

    /// <summary>
    /// 部分 Power 构造时会调用 HUD.skillPowers[slot].hideIcon()
    /// 这里把本地 HUD 对应槽位恢复，避免隐形 Hero 影响玩家 HUD
    /// </summary>
    protected static void RestoreHudIcon(int slot)
    {
        var hud = dc.ui.HUD.Class.ME;
        if (hud?.skillPowers == null || slot < 0 || slot >= hud.skillPowers.length)
        {
            return;
        }

        if (hud.skillPowers.getDyn(slot) is dc.ui.hud.Skill skill)
        {
            skill.showIcon();
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        try
        {
            this.Skills.Dispose();
        }
        finally
        {
            try
            {
                this.Weapons.Dispose();
            }
            finally
            {
                this.talismans.Dispose();
            }
        }
    }
}

internal sealed class ServerHeroActionRuntime : HeroActionRuntime
{
    private readonly GhostShield shield;

    public ServerHeroActionRuntime(Hero hero, string ownerGuid)
        : base(hero, ownerGuid) => this.shield = new GhostShield(this.Weapons);

    public bool IsSkillReady(int slot)
    {
        var skill = this.Skills.GetActive(slot)?.skill;
        return skill?.isReady() == true;
    }

    public bool CastSkill(int slot, double ratio)
    {
        var manager = this.Context.Hero.activeSkillsManager;

        var before = this.Context.Count();
        manager.onActiveSkill(slot, ratio);
        RestoreHudIcon(slot);
        this.Context.LogActionDelta("cast", slot, before);
        return true;
    }

    public bool PrepareShield(int slot, ItemRef? snapshot) => this.shield.Prepare(slot, snapshot);

    public bool UseShield(
        ShieldActionKind kind,
        int slot,
        double ratio,
        bool fullParry,
        AttackData? sourceAtk,
        double elapsedMs) => this.shield.Apply(kind, slot, ratio, fullParry, sourceAtk, elapsedMs);
}

internal sealed class ClientHeroActionRuntime(Hero hero, string ownerGuid) : HeroActionRuntime(hero, ownerGuid)
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ClientHeroActionRuntime");

    public bool ReplaySkill(int slot)
    {
        if (this.Context.Hero.activeSkillsManager is not BeheadedActiveSkillsManager manager)
        {
            return false;
        }

        var item = this.Context.Hero.inventory.getActiveOn(slot);

        this.Skills.GetActive(slot)?.useActiveItem(item);

        var data = item._itemData;

        var before = this.Context.Count();
        switch (data.group)
        {
            case 0://释放放置技能
                manager.spawnDeployedTrapSkill(slot, item, data);
                break;
            case 1://释放手榴弹技能
                manager.spawnGrenadeSkill(item, data);
                break;
            case 2:
                LogReplaySkip(slot, "sidekick");
                return true;
            case 3://释放能力技能

                if (data.props.mob != null)
                {
                    LogReplaySkip(slot, "pet");
                    return true;
                }

                manager.spawnPowerSkill(slot, item, data);
                break;
            default:
                return false;
        }

        RestoreHudIcon(slot);
        this.Context.LogActionDelta("replay", slot, before);
        return true;
    }

    private static void LogReplaySkip(int slot, string kind) => Logger.Debug(
            "GhostHero replay skipped: kind={Kind} slot={Slot}",
            kind, slot);
}
