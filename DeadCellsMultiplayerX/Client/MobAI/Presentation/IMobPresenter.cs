using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Client.MobAI.Presentation
{
    /// <summary>
    /// 客户端 Mob 表现接口。
    /// 只负责按服务端计划播放动画/技能，不修改权威状态。
    /// </summary>
    public interface IMobPresenter
    {
        /// <summary>
        /// 收到一个已提交的动作计划。
        /// </summary>
        void OnPlanCommitted(MobActionPlan plan);

        /// <summary>
        /// 收到阶段纠正/确认。
        /// </summary>
        void OnPhase(MobActionPhaseUpdate update);

        /// <summary>
        /// 动作被服务端取消。
        /// </summary>
        void OnCancelled(long actionId, MobActionCancelReason reason);
    }
}
