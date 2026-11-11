using System;
using System.Collections.Generic;
using dc;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Common.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Ghosts.Modules;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using Serilog;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.Execution
{
    /// <summary>
    /// 服务端释放主动技能模块。
    /// 思路：Prepare -> IsReady -> Cast，把技能就绪判断留在服务端；
    /// 新动作只需实现 IServerActionModule，不需要修改 ActionExecutor 的分发表。
    /// </summary>
    internal sealed class SkillActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.ActiveSkill, SlotPolicy.Active);

        public bool TryExecute(
            ServerGhostHero ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.PrepareSkill(request.Slot, request.Item))
                return false;

            if (!ghost.Actions.IsSkillReady(request.Slot))
            {
                Logger.Warning(
                    "Reject skill cast: guid={Guid} slot={Slot} not ready",
                    ghost.OwnerGuid, request.Slot);
                return false;
            }

            if (!ghost.Actions.CastSkill(request.Slot, ratio))
                return false;

            Logger.Information(
                "Skill cast accepted: guid={Guid} slot={Slot} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Slot, request.Item.ItemId, request.ActionId);

            return true;
        }
    }

    /// <summary>
    /// 服务端使用武器模块。
    /// 思路：Prepare -> Use，只负责在服务端 Ghost 上执行一次武器；
    /// 延迟补偿（MobRewind）由 ExecutionStage 统一包裹，模块本身不处理时钟回退。
    /// </summary>
    internal sealed class WeaponActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Weapon, SlotPolicy.Weapon);

        public bool TryExecute(
            ServerGhostHero ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.PrepareWeapon(request.Slot, request.Item))
                return false;

            if (!ghost.Actions.UseWeapon(request.Slot, request.Cycle))
                return false;

            Logger.Information(
                "Weapon use accepted: guid={Guid} slot={Slot} item={Item} cycle={Cycle} action={ActionId}",
                ghost.OwnerGuid, request.Slot, request.Item.ItemId,
                request.Cycle, request.ActionId);

            return true;
        }
    }

    /// <summary>
    /// 服务端装备项链模块。
    /// 思路：保留独立的项链动作作为兼容路径，重复应用由 GhostTalismans 的签名去重兜底；
    /// 后续 Loadout 版本化同步会成为项链的唯一主通道。
    /// </summary>
    internal sealed class TalismanActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Talisman, SlotPolicy.TalismanOnly);

        public bool TryExecute(
            ServerGhostHero ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.ApplyTalisman(request.Item))
            {
                Logger.Warning(
                    "Talisman rejected: guid={Guid} item={Item} action={ActionId}",
                    ghost.OwnerGuid, request.Item.ItemId, request.ActionId);
                return false;
            }

            Logger.Information(
                "Talisman accepted: guid={Guid} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Item.ItemId, request.ActionId);

            return true;
        }
    }

    internal sealed class ShieldActionHandler : IServerActionModule
    {
        private const long CounterDedupeMs = 250;

        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ServerGhostManager");

        private readonly ServerEntityRegistry entities;
        private readonly Func<long> clock;
        private readonly Dictionary<(string Guid, uint NetId), long> lastCounters = [];

        public ShieldActionHandler(
            ServerEntityRegistry entities,
            Func<long> clock)
        {
            this.entities = entities;
            this.clock = clock;
        }

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Shield, SlotPolicy.Weapon);

        public bool TryExecute(
            ServerGhostHero ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.PrepareShield(request.Slot, request.Item))
            {
                Logger.Warning(
                    "Shield rejected: guid={Guid} slot={Slot} item={Item} " +
                    "action={ActionId}",
                    ghost.OwnerGuid, request.Slot,
                    request.Item?.ItemId, request.ActionId);
                return false;
            }

            AttackData? sourceAtk = null;

            if (request.ShieldKind == ShieldActionKind.Counter)
            {
                if (request.SourceNetId == 0
                    || !entities.TryGetMob(request.SourceNetId, out var mob)
                    || mob == null
                    || mob.destroyed)
                {
                    Logger.Warning(
                        "Shield counter rejected: guid={Guid} source={Source} " +
                        "action={ActionId}",
                        ghost.OwnerGuid, request.SourceNetId, request.ActionId);
                    return false;
                }

                var key = (ghost.OwnerGuid, request.SourceNetId);
                long now = Environment.TickCount64;
                if (lastCounters.TryGetValue(key, out long last)
                    && now - last < CounterDedupeMs)
                {
                    return false;
                }

                lastCounters[key] = now;

                sourceAtk = new AttackData();
                sourceAtk.source = mob;
                sourceAtk.hitResult = new HitResult.Normal();
            }

            double elapsedMs = System.Math.Max(
                0, clock() - request.ClientTimeMs);

            if (!ghost.Actions.UseShield(
                    request.ShieldKind, request.Slot, ratio,
                    request.FullParry, sourceAtk, elapsedMs))
            {
                Logger.Warning(
                    "Shield action rejected: guid={Guid} kind={Kind} slot={Slot} " +
                    "action={ActionId}",
                    ghost.OwnerGuid, request.ShieldKind,
                    request.Slot, request.ActionId);
                return false;
            }

            Logger.Information(
                "Shield action accepted: guid={Guid} kind={Kind} slot={Slot} " +
                "fullParry={FullParry} source={Source} action={ActionId}",
                ghost.OwnerGuid, request.ShieldKind, request.Slot,
                request.FullParry, request.SourceNetId, request.ActionId);

            return true;
        }
    }
}
