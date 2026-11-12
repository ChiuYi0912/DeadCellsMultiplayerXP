using DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
namespace DeadCellsMultiplayerX.Server.Integration.Hooks
{
    /// <summary>
    /// Power Hook 路由：清理 owner 已销毁的 Ghost Power。
    /// </summary>
    internal sealed class ServerPowerHookRouter : IServerPowerHookSink
    {
        public void OnUpdate(Hook_Power.orig_update orig, Power self)
        {
            if (self == null)
                return;

            var owner = self.owner;
            if (owner == null || owner._level == null)
            {
                self.destroy();
                return;
            }

            orig(self);
        }
    }
}
