namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.hl.types;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;

/// <summary>
/// 关卡区域 Hook 路由：登记服务端运行时创建的动态区域。
/// </summary>
internal sealed class ServerLevelHookRouter(ServerMain server) : IServerLevelHookSink
{
    private readonly ServerMain server = server;

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
        this.server.Session?.AreaAffects.Register(self);
    }
}
