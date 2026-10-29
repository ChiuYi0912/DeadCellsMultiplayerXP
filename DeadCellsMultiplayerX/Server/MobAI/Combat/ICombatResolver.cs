using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Server.MobAI.Combat
{
    /// <summary>
    /// 服务端战斗结算器。
    /// 命中判定与伤害提交分离，并处理晚到的防御输入。
    /// </summary>
    public interface ICombatResolver
    {
        /// <summary>
        /// 登记一个命中票据。
        /// </summary>
        HitTicket RegisterHit(HitTicket ticket);

        /// <summary>
        /// 处理客户端防御上报；返回 null 表示尚未裁决。
        /// </summary>
        CombatResolution? ReportDefense(DefenseReport report);

        /// <summary>
        /// 推进结算，返回本 Tick 产生的裁决。
        /// </summary>
        IReadOnlyList<CombatResolution> Tick(long serverTick, long serverTimeMs);
    }
}
