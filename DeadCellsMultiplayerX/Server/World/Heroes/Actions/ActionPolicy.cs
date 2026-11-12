namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions;

/// <summary>
/// 服务端 Ghost 动作的准入参数
/// </summary>
internal static class ActionPolicy
{
    public const double MAX_CAST_DISTANCE_SQ = 48.0 * 48.0;
    public const int MAX_TIER = 255;
}
