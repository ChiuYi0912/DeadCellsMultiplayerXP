using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using Serilog;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Client.MobAI.Modifiers.Zombie
{
    /// <summary>
    /// Zombie 客户端修改器占位实现。
    /// 当前保持现有 AI 锁定行为，不接管动画/命中
    /// </summary>
    internal sealed class ZombieClientModifier : MobModifierBase<GameZombie>
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ZombieClientModifier");

        private readonly GuestClientSession session;
        private readonly RemoteWorld world;

        public ZombieClientModifier(
            GuestClientSession session,
            RemoteWorld world)
        {
            this.session = session;
            this.world = world;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnAiLocked(_ => true);
            hooks.OnClientUpdate(_ => false);
        }

        /// <inheritdoc />
        public override void Attach(GameZombie mob, IMobModifierContext context)
        {
            Logger.Debug(
                "Zombie client modifier attached: netId={NetId} time={Time} local={Local}",
                context.NetId, session.SyncedTimeMs, world.LocalGuid);
        }

        /// <inheritdoc />
        public override void Detach(GameZombie mob)
        {
            Logger.Debug("Zombie client modifier detached: mob={Mob}", mob.GetType().Name);
        }
    }
}
