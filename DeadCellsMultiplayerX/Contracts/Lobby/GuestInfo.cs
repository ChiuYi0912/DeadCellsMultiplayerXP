namespace DeadCellsMultiplayerX.Contracts.Lobby;

/// <summary>
/// 房间中每位玩家的信息
/// </summary>
public class GuestInfo
{
    /// <summary>
    /// 玩家名称
    /// </summary>
    public string Name { get; set; } = "Guest";

    /// <summary>
    /// 玩家 Id
    /// </summary>
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();

    /// <summary>
    /// 是否准备好游戏
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// 是否是房主
    /// </summary>
    public bool IsHost { get; set; }

    /// <summary>
    /// 是否已连接服务器
    /// </summary>
    public bool IsConnectedServer { get; set; }

    /// <summary>
    /// 玩家皮肤
    /// </summary>
    public string SkinMould { get; set; } = string.Empty;


    public GuestInfo Clone() => (GuestInfo)this.MemberwiseClone();
}
