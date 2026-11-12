using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using Serilog;
using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution
{
    internal sealed class WeaponActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Weapon, SlotPolicy.Weapon);

        public bool TryExecute(
            ServerHeroCombatProxy ghost,
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
}
