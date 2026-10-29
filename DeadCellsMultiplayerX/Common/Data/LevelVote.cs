using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data
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