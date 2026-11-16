namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 客户端上报的一次英雄动作意图。
/// 只描述“想做什么”，不携带物品、等级、位置等本地执行结果。
/// </summary>
[MessagePackObject]
public class HeroCommand
{
    [Key(0)] public int Epoch { get; set; }

    /// <summary>
    /// 发送者内单调递增；服务端按发送者去重。
    /// </summary>
    [Key(1)] public long CommandId { get; set; }

    [Key(2)] public HeroActionKind Kind { get; set; }

    [Key(3)] public int Slot { get; set; }

    [Key(4)] public HeroIntent Intent { get; set; } = new();

    [Key(5)] public long ClientTimeMs { get; set; }

    [Key(6)] public string LevelId { get; set; } = string.Empty;

    [Key(7)] public int SubLevelIndex { get; set; } = -1;
}
