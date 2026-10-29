using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Server.MobAI.Scheduling
{
    /// <summary>
    /// 服务端 Mob 动作调度器。
    /// 负责把意图计划分配到 Tick，并保证每个 mob 的并发动作上限。
    /// </summary>
    public interface IMobActionScheduler
    {
        /// <summary>
        /// 调度一个动作计划。
        /// </summary>
        bool Schedule(MobActionPlan plan);

        /// <summary>
        /// 取消指定动作。
        /// </summary>
        bool Cancel(long actionId, MobActionCancelReason reason);

        /// <summary>
        /// 查询指定 mob 当前的活动计划。
        /// </summary>
        bool TryGetActive(uint mobNetId, out MobActionPlan? plan);
    }
}
