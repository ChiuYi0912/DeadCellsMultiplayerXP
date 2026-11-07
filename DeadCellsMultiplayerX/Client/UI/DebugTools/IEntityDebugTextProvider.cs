using DeadCellsMultiplayerX.Client.Replication.Entities;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Client.UI.DebugTools
{
    internal interface IEntityDebugTextProvider
    {
        /// <summary>
        /// 负责的实体类型
        /// </summary>
        EntityKind Kind { get; }

        /// <summary>
        /// 标签颜色，默认白色
        /// </summary>
        int LabelColor => 0xFFFFFF;

        /// <summary>
        /// 实体事件；默认忽略
        /// </summary>
        void OnEvent(MobModifierEvent evt) { }


        string? GetText(
            uint netId, object native, ClientEntityContext ctx, long nowMs);

        void Reset() { }
    }
}
