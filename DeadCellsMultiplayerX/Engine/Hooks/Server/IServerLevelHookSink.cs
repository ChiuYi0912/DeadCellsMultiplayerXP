using dc.hl.types;

using dc.tool;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 关卡区域 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerLevelHookSink
    {
        void OnAreaAffectInit(
            Hook_LevelAreaAffect.orig_init orig,
            LevelAreaAffect self,
            int x,
            int y,
            double durationS,
            int a,
            double aDurationS,
            double aValue,
            ArrayObj affixes);
    }
}
