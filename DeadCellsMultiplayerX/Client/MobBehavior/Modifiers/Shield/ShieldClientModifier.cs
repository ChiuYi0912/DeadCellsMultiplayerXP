using dc.en;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using ModCore.Utilities;
using Serilog;
using GameShield = dc.en.mob.Shield;
using dc.hl.types;

namespace DeadCellsMultiplayerX.Client.MobBehavior.Modifiers.Shield
{
    internal sealed class ShieldClientModifier : MobModifierBase<GameShield>
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ShieldClientModifier");

        private readonly GuestClientSession session;
        private readonly RemoteWorld world;

        public ShieldClientModifier(
            GuestClientSession session,
            RemoteWorld world)
        {
            this.session = session;
            this.world = world;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnInitSkills(OnInitSkills);
            hooks.OnClientEventHandled(_ => false);
        }

        private void OnInitSkills(
            MobHookContext context, ArrayObj oldSkills, ArrayObj skills)
        {

        }
    }
}
