using dc.hl.types;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;

using dc.tool;
namespace DeadCellsMultiplayerX.Server.Integration.Hooks
{
    /// <summary>
    /// 关卡区域 Hook 路由：登记服务端运行时创建的动态区域。
    /// </summary>
    internal sealed class ServerLevelHookRouter : IServerLevelHookSink
    {
        private readonly ServerMain server;

        public ServerLevelHookRouter(ServerMain server)
        {
            this.server = server;
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
            server.Session?.AreaAffects.Register(self);
        }
    }
}
