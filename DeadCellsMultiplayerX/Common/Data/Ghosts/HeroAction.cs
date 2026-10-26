using System.Collections.Generic;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Ghosts
{
    /// <summary>
    /// 由客户端上报、服务端执行并广播的英雄动作类型
    /// </summary>
    public enum HeroActionKind
    {
        ActiveSkill = 0,
        Weapon = 1,
        Talisman = 2,
    }

    /// <summary>
    /// 物品的可传输快照
    /// </summary>
    [MessagePackObject]
    public class ItemRef
    {
        [Key(0)] public int Kind { get; set; }

        [Key(1)] public string ItemId { get; set; } = string.Empty;

        [Key(2)] public int ItemLevel { get; set; } = 1;

        [Key(3)] public int PosID { get; set; } = -1;

        [Key(4)] public int PermanentId { get; set; }

        [Key(5)] public int Ammo { get; set; }

        [Key(6)] public List<string> Affixes { get; set; } = [];

        [Key(7)] public List<string> InactiveAffixes { get; set; } = [];
    }

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
    }

    /// <summary>
    /// 服务端确认并广播的一次英雄动作
    /// </summary>
    [MessagePackObject]
    public class HeroActionEvent
    {
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// 与 HeroAction.ActionId 一致 用于客户端去重
        /// </summary>
        [Key(1)] public long ActionId { get; set; }

        [Key(2)] public string CasterGuid { get; set; } = string.Empty;

        [Key(3)] public HeroActionKind Kind { get; set; }

        [Key(4)] public int Slot { get; set; }

        [Key(5)] public ItemRef Item { get; set; } = new();

        [Key(6)] public int CX { get; set; }
        [Key(7)] public int CY { get; set; }
        [Key(8)] public double XR { get; set; }
        [Key(9)] public double YR { get; set; }
        [Key(10)] public int Dir { get; set; }
        [Key(11)] public double DX { get; set; }
        [Key(12)] public double DY { get; set; }

        [Key(13)] public double Ratio { get; set; } = 1.0;
        [Key(14)] public int Cycle { get; set; }

        [Key(15)] public int BrutalityTier { get; set; }
        [Key(16)] public int SurvivalTier { get; set; }
        [Key(17)] public int TacticTier { get; set; }

        [Key(18)] public string LevelId { get; set; } = string.Empty;
        [Key(19)] public int SubLevelIndex { get; set; } = -1;
        [Key(20)] public long ServerTimeMs { get; set; }
    }
}
