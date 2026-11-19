namespace DeadCellsMultiplayerX.Contracts.Levels;

/// <summary>
/// 用于大关卡切换时发送的数据
/// </summary>
public class LevelVote
{
    public string PlyerID { get; set; } = string.Empty;

    public string NextLevelID { get; set; } = "PrisonStart";

    public bool LevelSelected { get; set; }

    /// <summary>投票所属的当前关卡，缺失时拒绝</summary>
    public int Epoch { get; set; } = -1;
}
