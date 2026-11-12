using DeadCellsMultiplayerX.Contracts.Snapshots;

namespace DeadCellsMultiplayerX.Server.World.Actors
{
    /// <summary>
    /// 把客户端上传的 HeroDynamic 镜像到服务端 Ghost。
    /// </summary>
    internal static class ServerHeroStateMirror
    {
        /// <summary>
        /// 应用位置、等级与生命状态；返回该 Ghost 是否可参与战斗。
        /// </summary>
        public static bool Apply(ServerHeroCombatProxy ghost, HeroDynamic state)
        {
            if (ghost == null || state?.Pos == null)
                return false;

            ghost.SetTransform(
                state.Pos.CX,
                state.Pos.CY,
                state.Pos.XR,
                state.Pos.XY,
                state.Pos.DIR,
                state.Pos.DX,
                state.Pos.DY);
            ghost.SyncLevel();
            ghost.SetServerVitals();

            bool combat = !state.IsDead && state.Life > 0;
            ghost.SetCombatEnabled(combat);
            return combat;
        }
    }
}
