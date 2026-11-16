namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 结果来源：客户端命令，或服务端世界事件（例如盾牌格挡/反击）。
/// </summary>
public enum HeroOutcomeCause
{
    Command = 0,
    WorldEvent = 1,
}
