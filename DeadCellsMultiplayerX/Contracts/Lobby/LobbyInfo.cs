namespace DeadCellsMultiplayerX.Contracts.Lobby;

/// <summary>
/// 房间的信息
/// </summary>
public class LobbyInfo
{
    /// <summary>
    /// 房间里的玩家
    /// </summary>
    public Dictionary<string, GuestInfo> Guests { get; set; } = [];

    /// <summary>
    /// 房间是否已开始游戏
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// 房间已开始游戏且Guest可以连接服务器
    /// </summary>
    public bool CanConnectServer { get; set; }

    /// <summary>
    /// 房间 Host
    /// </summary>
    public string Owner { get; set; } = "";

    /// <summary>
    /// 游戏模式
    /// </summary>
    public GameMode GameMode { get; set; } = GameMode.None;
}
