using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Client.MobAI.Prediction
{
    /// <summary>
    /// 客户端预测对账器。
    /// 用服务端最终裁决纠正本地预测，必要时回滚表现/生命。
    /// </summary>
    public interface IPredictionReconciler
    {
        /// <summary>
        /// 应用一条服务端裁决。
        /// </summary>
        void Apply(CombatResolution resolution);
    }
}
