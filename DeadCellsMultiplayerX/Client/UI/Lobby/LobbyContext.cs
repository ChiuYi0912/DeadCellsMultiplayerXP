namespace DeadCellsMultiplayerX.Client.UI.Lobby;

using DeadCellsMultiplayerX.Contracts.Lobby;

/// <summary>
/// 大厅共享上下文
/// </summary>
internal sealed class LobbyContext
{
    public string LocalGuid { get; set; } = string.Empty;
    public GuestInfo? Me { get; set; }   //当前的客户
    public LobbyInfo? Lobby { get; set; } //联机大厅数据
    public bool IsHost { get; set; } //是否是房主

    public long LatencyMs { get; set; } //与服务器的延迟
    public string HostIp { get; set; } = string.Empty; //ip
    public int HostPort { get; set; } //端口
    public string? LastError { get; set; } //报错文本

    public void Reset()
    {
        this.Me = null;
        this.Lobby = null;
        this.IsHost = false;
        this.LastError = null;
        this.LatencyMs = 0;
    }
}
