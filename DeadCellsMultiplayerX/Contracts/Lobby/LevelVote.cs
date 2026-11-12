namespace DeadCellsMultiplayerX.Contracts.Lobby
{
    /// <summary>
    /// 用于大关卡切换时发送的数据
    /// </summary>
    public class LevelVote
    {
        public string PlyerID { get; set; } = string.Empty;

        public string NextLevelID { get; set; } = "PrisonStart";

        public bool LevelSelected { get; set; } = false;
    }
}
