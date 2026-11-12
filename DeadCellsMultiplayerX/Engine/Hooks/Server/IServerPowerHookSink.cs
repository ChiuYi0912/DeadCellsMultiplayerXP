namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;

/// <summary>
/// Power Hook 的端侧接收者。
/// </summary>
internal interface IServerPowerHookSink
{
    public void OnUpdate(Hook_Power.orig_update orig, Power self);
}
