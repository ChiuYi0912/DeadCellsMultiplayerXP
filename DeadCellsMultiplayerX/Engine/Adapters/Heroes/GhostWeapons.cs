namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using dc.tool;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// GhostHero的武器
/// </summary>
internal sealed class GhostWeapons(GhostActionContext context)
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostWeapons");

    private readonly GhostActionContext context = context;
    private readonly GhostLoadout loadout = new GhostLoadout(context, InventItemKind.Indexes.Weapon);
    private readonly Weapon?[] weapons = new Weapon?[2];

    public Weapon? Get(int slot) => slot is >= 0 and <= 1 ? this.weapons[slot] : null;

    /// <summary>
    /// 确保武器槽物品存在并创建具体 Weapon 子类
    /// </summary>
    public bool Prepare(int slot, ItemRef? snapshot)
    {
        if (!this.loadout.TryPrepare(slot, snapshot, out var item, out var changed))
        {
            return false;
        }

        if (changed || this.weapons[slot] == null)
        {
            if (item == null)
            {
                return false;
            }

            Weapon? weapon = Weapon.Class.create.Invoke(this.context.Hero, item);

            // 防止其中一位玩家使用模组武器
            if (weapon == null)
            {
                Logger.Warning(
                    "GhostWeapons {Owner}: unknown weapon {ItemId}",
                    this.context.OwnerGuid, item.kind.Index);
                return false;
            }

            this.DisposeWeapon(slot);
            this.weapons[slot] = weapon;
        }

        return this.weapons[slot] != null;
    }


    /// <summary>
    /// 执行一次武器连招段；PresentOnly 模式下所有命中被显式降级为表现。
    /// </summary>
    public bool Use(int slot, int cycle, HitPresentationMode mode)
    {
        var weapon = this.weapons[slot];
        if (weapon == null)
        {
            return false;
        }

        weapon.set_cycle(cycle);

        var before = this.context.Count();
        var ranged = weapon.isBow() || weapon.item?.hasTag("Ranged".AsHaxeString()) == true;
        var candidates = this.CaptureOpponents(weapon, slot, ranged);

        using var scope = mode == HitPresentationMode.PresentOnly
            ? HitPresentationScope.Begin()
            : null;

        weapon._executeImpl(1.0);

        if (!ranged)
        {
            this.FallbackHits(weapon, candidates, slot);
        }

        this.context.LogActionDelta(
            mode == HitPresentationMode.PresentOnly ? "replay" : "weapon",
            slot, before);
        return true;
    }

    private List<(dc.en.Mob Mob, int Life)> CaptureOpponents(
        Weapon weapon, int slot, bool ranged)
    {
        var result = new List<(dc.en.Mob, int)>();
        var seen = new HashSet<dc.en.Mob>();
        var level = this.context.Hero._level;
        if (level == null)
        {
            return result;
        }

        this.CollectFromTeam(level.teamMob, weapon, result, seen);
        this.CollectFromTeam(level.teamProp, weapon, result, seen);

        var entities = level.entities;
        if (entities != null)
        {
            for (var i = 0; i < entities.length; i++)
            {
                var mob = AffectCodec.ToObject<dc.en.Mob>(entities.getDyn(i));
                AddCandidate(mob, weapon, result, seen);
            }
        }

        Logger.Verbose(
            "GhostHero weapon targets: owner={Owner} slot={Slot} item={Item} " +
            "teamMob={TeamMob} teamProp={TeamProp} candidates={Candidates} ranged={Ranged}",
            this.context.OwnerGuid, slot, weapon.item?.kind.Index,
            level.teamMob?.asMobs?.length ?? -1,
            level.teamProp?.asMobs?.length ?? -1,
            result.Count, ranged);

        return result;
    }

    private void CollectFromTeam(
        Team? team,
        Weapon weapon,
        List<(dc.en.Mob Mob, int Life)> result,
        HashSet<dc.en.Mob> seen)
    {
        var mobs = team?.asMobs;
        if (mobs == null)
        {
            return;
        }

        for (var i = 0; i < mobs.length; i++)
        {
            var mob = AffectCodec.ToObject<dc.en.Mob>(mobs.getDyn(i));
            AddCandidate(mob, weapon, result, seen);
        }
    }

    private void AddCandidate(
        dc.en.Mob? mob,
        Weapon weapon,
        List<(dc.en.Mob Mob, int Life)> result,
        HashSet<dc.en.Mob> seen)
    {
        if (mob == null || mob.destroyed || mob.life <= 0)
        {
            return;
        }

        if (!seen.Add(mob))
        {
            return;
        }

        if (ReferenceEquals(mob._team, this.context.Hero._team))
        {
            return;
        }

        if (!weapon.canHit(mob, null))
        {
            return;
        }

        result.Add((mob, mob.life));
    }

    private void FallbackHits(
        Weapon weapon,
        List<(dc.en.Mob Mob, int Life)> candidates,
        int slot)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        var hits = 0;
        foreach (var (mob, life) in candidates)
        {
            if (mob.destroyed || mob.life != life)
            {
                continue;
            }

            if (!weapon.canHit(mob, null))
            {
                continue;
            }

            // 服务端真实结算；客户端在 HitPresentationScope 内只触发受击表现。
            weapon.hitFromWeapon(mob, Ref<int>.In(-1));
            hits++;
            if (hits > 0)
            {
                Logger.Verbose(
                    "GhostHero weapon fallback: owner={Owner} slot={Slot} item={Item} hits={Hits}",
                    this.context.OwnerGuid, slot, weapon.item?.kind.Index, hits);
            }
        }
    }

    private void DisposeWeapon(int slot)
    {
        var weapon = this.weapons[slot];
        if (weapon == null)
        {
            return;
        }

        weapon.dispose();

        this.weapons[slot] = null;
    }

    public void Dispose()
    {
        for (var i = 0; i < this.weapons.Length; i++)
        {
            this.DisposeWeapon(i);
        }

        this.loadout.Clear();
    }

}
