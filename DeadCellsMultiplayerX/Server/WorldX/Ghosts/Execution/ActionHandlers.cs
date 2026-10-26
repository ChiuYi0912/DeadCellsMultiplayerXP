using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Ghosts;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Execution
{
    /// <summary>
    /// 服务端释放主动技能
    /// </summary>
    internal sealed class SkillActionHandler : IActionHandler
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public HeroActionKind Kind => HeroActionKind.ActiveSkill;

        public bool TryExecute(
            GhostHero ghost,
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
    /// 服务端使用武器
    /// </summary>
    internal sealed class WeaponActionHandler : IActionHandler
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public HeroActionKind Kind => HeroActionKind.Weapon;

        public bool TryExecute(
            GhostHero ghost,
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
    /// 服务端装备项链
    /// </summary>
    internal sealed class TalismanActionHandler : IActionHandler
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        public HeroActionKind Kind => HeroActionKind.Talisman;

        public bool TryExecute(
            GhostHero ghost,
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
}
