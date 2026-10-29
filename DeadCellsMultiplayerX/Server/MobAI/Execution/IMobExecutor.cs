using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Server.MobAI.Execution
{
    /// <summary>
    /// 服务端 Mob 动作执行器。
    /// 唯一允许调用原版技能/动画/碰撞的抽象入口。
    /// </summary>
    public interface IMobExecutor
    {
        /// <summary>
        /// 开始执行一个动作计划。
        /// </summary>
        bool TryStart(MobActionPlan plan);

        /// <summary>
        /// 取消一个正在执行的动作。
        /// </summary>
        void Cancel(long actionId, MobActionCancelReason reason);
    }
}
