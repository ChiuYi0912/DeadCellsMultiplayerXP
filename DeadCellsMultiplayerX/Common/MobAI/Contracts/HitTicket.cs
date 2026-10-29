namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// 命中来源类型。
    /// </summary>
    public enum HitKind : byte
    {
        /// <summary>
        /// 近战命中。
        /// </summary>
        Melee = 0,

        /// <summary>
        /// 投射物命中。
        /// </summary>
        Projectile = 1,

        /// <summary>
        /// 区域命中。
        /// </summary>
        Area = 2,

        /// <summary>
        /// 接触伤害。
        /// </summary>
        Contact = 3,
    }

    /// <summary>
    /// 服务端命中票据。
    /// 命中判定成立时创建，伤害在 <see cref="ResolveTick"/> 才提交，
    /// 中间允许盾反/闪避输入取消。
    /// </summary>
    public sealed class HitTicket
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// epoch 内唯一的命中 id。
        /// </summary>
        public long HitId { get; init; }

        /// <summary>
        /// 所属动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 攻击者 mob。
        /// </summary>
        public uint MobNetId { get; init; }

        /// <summary>
        /// 被攻击玩家 guid。
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 被攻击实体 NetId。
        /// </summary>
        public uint TargetNetId { get; init; }

        /// <summary>
        /// 命中判定 Tick。
        /// </summary>
        public long HitTick { get; init; }

        /// <summary>
        /// 伤害提交 Tick。
        /// </summary>
        public long ResolveTick { get; init; }

        /// <summary>
        /// 服务端计算出的伤害。
        /// </summary>
        public int Damage { get; init; }

        /// <summary>
        /// 命中来源。
        /// </summary>
        public HitKind Kind { get; init; }

        /// <summary>
        /// 命中发生时所在关卡。
        /// </summary>
        public string LevelId { get; init; } = string.Empty;

        /// <summary>
        /// 命中发生时所在子关卡。
        /// </summary>
        public int SubLevelIndex { get; init; } = -1;
    }
}
