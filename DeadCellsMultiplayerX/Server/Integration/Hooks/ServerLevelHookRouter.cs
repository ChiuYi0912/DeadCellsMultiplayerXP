namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.cine;
using dc.hl.types;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Engine.Adapters.Levels;
using DeadCellsMultiplayerX.Server;

/// <summary>
/// 转交主关卡加载完成通知，并登记服务端运行时创建的动态区域。
/// </summary>
internal sealed class ServerLevelHookRouter(ServerRuntime server) : IServerLevelHookSink
{
    private readonly ServerRuntime server = server;

    public void OnLoadNewLevel(Hook_LevelTransition.orig_loadNewLevel orig, LevelTransition self)
    {
        this.server.Worlds?.OnLevelLoading(self);
        NativeLevelTransition.Load(self);
    }

    public void OnAreaAffectInit(
        Hook_LevelAreaAffect.orig_init orig,
        LevelAreaAffect self,
        int x,
        int y,
        double durationS,
        int a,
        double aDurationS,
        double aValue,
        ArrayObj affixes)
    {
        orig(self, x, y, durationS, a, aDurationS, aValue, affixes);
        this.server.Worlds?.CurrentWorld?.AreaAffects.Register(self);
    }
}
