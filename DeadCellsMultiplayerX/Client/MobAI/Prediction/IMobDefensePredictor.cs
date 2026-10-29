using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Client.MobAI.Prediction
{
    /// <summary>
    /// 客户端防御预测上下文。
    /// </summary>
    public sealed class DefensePredictionContext
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// 动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 对应动作计划。
        /// </summary>
        public MobActionPlan Plan { get; init; } = new();

        /// <summary>
        /// 当前本地 Tick。
        /// </summary>
        public long LocalTick { get; init; }

        /// <summary>
        /// 玩家输入发生的 Tick。
        /// </summary>
        public long InputTick { get; init; }

        /// <summary>
        /// 防御类型。
        /// </summary>
        public DefenseKind Kind { get; init; }

        /// <summary>
        /// 输入朝向。
        /// </summary>
        public int Dir { get; init; }

        /// <summary>
        /// 输入序号。
        /// </summary>
        public long InputSequence { get; init; }
    }

    /// <summary>
    /// 客户端本地防御预测器。
    /// 只生成本地预测和上报数据，不决定最终伤害。
    /// </summary>
    public interface IMobDefensePredictor
    {
        /// <summary>
        /// 计算一次本地防御预测；返回 null 表示输入不在窗口内。
        /// </summary>
        DefenseReport? Evaluate(DefensePredictionContext context);
    }
}
