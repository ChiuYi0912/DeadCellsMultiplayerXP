namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

/// <summary>
/// 本地 Hero 的状态快照
/// </summary>
/// <remarks>
/// 唯一实例位于 <see cref="LocalHeroState"/> 中
/// </remarks>
internal sealed class LocalHeroContext
{
    /// <summary>
    /// 是否正在子关卡中
    /// </summary>
    public bool InSubLevel { get; set; }

    /// <summary>
    /// 当前 level id
    /// </summary>
    public string CurrentLevelId { get; set; } = string.Empty;

    /// <summary>
    /// 是否在过场动画
    /// </summary>
    public bool InCinematic { get; set; }

    /// <summary>
    /// 当前 sublevel 索引
    /// </summary>
    public int SubLevelIndex { get; set; } = -1;

    /// <summary>
    /// 本地 Hero 是否存在
    /// </summary>
    public bool HasHero { get; set; }

    /// <summary>
    /// 本地 Hero 是否活着
    /// </summary>
    public bool IsAlive { get; set; }
}
