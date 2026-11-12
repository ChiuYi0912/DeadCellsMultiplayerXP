using dc.libs.heaps.slib;

using dc;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 实体级 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerEntityHookSink
    {
        void OnSetColorMap(
            Hook_Entity.orig_setColorMap orig,
            Entity self,
            dc.String model,
            dc.String skin,
            HSprite sspr);

        bool OnIsOnScreen(Hook_Entity.orig__isOnScreen orig, Entity self);

        bool OnCanBeDetected(Hook_Entity.orig_canBeDetected orig, Entity self);

        void OnDispose(Hook_Entity.orig_dispose orig, Entity self);
    }
}
