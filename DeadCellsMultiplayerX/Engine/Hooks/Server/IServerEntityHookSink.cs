namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
using dc.libs.heaps.slib;

/// <summary>
/// 实体级 Hook 的端侧接收者。
/// </summary>
internal interface IServerEntityHookSink
{
    public void OnSetColorMap(
        Hook_Entity.orig_setColorMap orig,
        Entity self,
        String model,
        String skin,
        HSprite sspr);

    public bool OnIsOnScreen(Hook_Entity.orig__isOnScreen orig, Entity self);

    public bool OnCanBeDetected(Hook_Entity.orig_canBeDetected orig, Entity self);

    public void OnDispose(Hook_Entity.orig_dispose orig, Entity self);
}
