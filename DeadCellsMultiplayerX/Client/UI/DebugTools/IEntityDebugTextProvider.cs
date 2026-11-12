namespace DeadCellsMultiplayerX.Client.UI.DebugTools;

using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;

internal interface IEntityDebugTextProvider
{
    /// <summary>
    /// 负责的实体类型
    /// </summary>
    public EntityKind Kind { get; }

    /// <summary>
    /// 标签颜色，默认白色
    /// </summary>
    public int LabelColor => 0xFFFFFF;

    /// <summary>
    /// 实体事件；默认忽略
    /// </summary>
    public void OnEvent(EntityEvent evt) { }


    public string? GetText(uint netId, object native, long nowMs);

    public void Reset() { }
}
