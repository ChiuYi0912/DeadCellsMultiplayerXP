namespace DeadCellsMultiplayerX.Server.World.Ghosts
{
    /// <summary>
    /// 服务端 Ghost 动作的准入参数
    /// </summary>
    internal static class ActionPolicy
    {
        public const double MaxCastDistanceSq = 48.0 * 48.0;
        public const int MaxTier = 255;
    }
}
