namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.cine;
using dc.hl.types;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;
using HaxeProxy.Runtime;

/// <summary>
/// 转交主关卡加载完成通知，并登记服务端运行时创建的动态区域。
/// </summary>
internal sealed class ServerLevelHookRouter(ServerMain server) : IServerLevelHookSink
{
    private readonly ServerMain server = server;

    public void OnLoadMainLevel(Hook_Game.orig_loadMainLevel orig, Game self,
        LevelTransition transition, dc.String levelId, Ref<bool> reload, int? seed)
    {
        orig(self, transition, levelId, reload, seed);
        this.server.Worlds?.OnMainLevelLoaded(self, transition, levelId.ToString());
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
        this.server.Session?.AreaAffects.Register(self);
    }
}
