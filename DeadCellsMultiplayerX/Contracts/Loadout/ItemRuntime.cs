using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Loadout
{
    [MessagePackObject]
    public sealed class ItemRuntime
    {
        [Key(0)] public int Ammo { get; set; }

        /// <summary>
        /// 服务端时间毫秒；0 表示无冷却。
        /// </summary>
        [Key(1)] public double CooldownUntilMs { get; set; }
    }
}
