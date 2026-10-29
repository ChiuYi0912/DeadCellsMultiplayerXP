using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Client.MobAI.Events
{
    /// <summary>
    /// 客户端 mob 修改器事件总线
    /// </summary>
    public interface IClientMobEventBus
    {
        /// <summary>
        /// 发布事件
        /// </summary>
        void Publish(MobModifierEvent evt);

        /// <summary>
        /// 订阅事件
        /// </summary>
        void Subscribe(Action<MobModifierEvent> handler);

        /// <summary>
        /// 取出当前事件
        /// </summary>
        IReadOnlyList<MobModifierEvent> Drain();

        /// <summary>
        /// 清空
        /// </summary>
        void Clear();
    }
}
