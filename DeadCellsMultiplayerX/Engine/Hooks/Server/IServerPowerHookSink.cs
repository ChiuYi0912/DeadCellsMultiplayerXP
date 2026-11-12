using dc;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// Power Hook 的端侧接收者。
    /// </summary>
    internal interface IServerPowerHookSink
    {
        void OnUpdate(Hook_Power.orig_update orig, Power self);
    }
}
