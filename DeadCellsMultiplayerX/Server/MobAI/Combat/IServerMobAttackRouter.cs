using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Contracts;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Server.MobAI.Combat
{
    /// <summary>
    /// 服务端 mob 攻击释放路由
    /// 接收修改器产生的动作计划，并在服务端 Tick 中执行/转发
    /// </summary>
    public interface IServerMobAttackRouter
    {
        /// <summary>
        /// 接收一次释放出来的攻击计划
        /// </summary>
        void Submit(MobActionPlan plan);

        /// <summary>
        /// 推进攻击执行，返回本 Tick 产生的事件
        /// </summary>
        IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs);

        /// <summary>
        /// 世界重置
        /// </summary>
        void Clear();
    }
}
