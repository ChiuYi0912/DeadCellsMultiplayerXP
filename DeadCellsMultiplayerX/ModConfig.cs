using ModCore.Storage;

namespace DeadCellsMultiplayerX
{
    internal class ModConfig
    {
        public static Config<ModConfig> Config { get; } = new("DeadCellsMultiplayerX");

        /// <summary>
        /// 开始游戏等待的最大时间
        /// </summary>
        public double StartTimeout { get; set; } = 5;

        /// <summary>
        /// 保存自定义按键
        /// </summary>
        public Dictionary<int, CoreLibrary.Utilities.ContorlLbleKeyConfig> ControlKeys { get; set; } = new();

        public int CurrentMode { get; set; } = 0;
    }
}
