namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.cine;
using dc.hl.types;
using dc.pr;
using dc.tool;
using HaxeProxy.Runtime;

/// <summary>
/// 主关卡加载与关卡区域 Hook 的端侧接收者。
/// </summary>
internal interface IServerLevelHookSink
{
    public void OnLoadMainLevel(Hook_Game.orig_loadMainLevel orig, Game self,
        LevelTransition transition, dc.String levelId, Ref<bool> reload, int? seed);

    public void OnAreaAffectInit(
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
