using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Actions
{
    /// <summary>
    /// 客户端上报的一次英雄技能或武器
    /// </summary>
    [MessagePackObject]
    public class HeroAction
    {
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// 发送者内单调递增 服务端按发送者校验
        /// </summary>
        [Key(1)] public long ActionId { get; set; }

        [Key(2)] public HeroActionKind Kind { get; set; }

        [Key(3)] public int Slot { get; set; }

        [Key(4)] public ItemRef Item { get; set; } = new();

        [Key(5)] public int CX { get; set; }
        [Key(6)] public int CY { get; set; }
        [Key(7)] public double XR { get; set; }
        [Key(8)] public double YR { get; set; }
        [Key(9)] public int Dir { get; set; }
        [Key(10)] public double DX { get; set; }
        [Key(11)] public double DY { get; set; }

        [Key(12)] public double Ratio { get; set; } = 1.0;

        [Key(13)] public int Cycle { get; set; }

        [Key(14)] public int TargetNetId { get; set; }

        [Key(15)] public long ClientTimeMs { get; set; }

        [Key(16)] public string LevelId { get; set; } = string.Empty;
        [Key(17)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// 蛮力等级
        /// </summary>
        [Key(18)] public int BrutalityTier { get; set; }

        /// <summary>
        /// 生存等级
        /// </summary>
        [Key(19)] public int SurvivalTier { get; set; }

        /// <summary>
        /// 战术等级
        /// </summary>
        [Key(20)] public int TacticTier { get; set; }

        /// <summary>
        /// 盾牌动作类型
        /// </summary>
        [Key(21)] public ShieldActionKind ShieldKind { get; set; }

        /// <summary>
        /// 是否完美弹反
        /// </summary>
        [Key(22)] public bool FullParry { get; set; }

        /// <summary>
        /// 触发格挡的实体 NetId
        /// </summary>
        [Key(23)] public uint SourceNetId { get; set; }

        /// <summary>
        /// 攻击方的 ActionId
        /// </summary>
        [Key(24)] public long SourceActionId { get; set; }
    }
}
