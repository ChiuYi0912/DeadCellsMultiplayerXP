using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Server.MobAI.Events
{
    /// <summary>
    /// 服务端 mob 修改器事件总线。
    /// 当前只在进程内使用；未来由 WorldFrame 广播。
    /// </summary>
    public interface IServerMobEventBus
    {
        /// <summary>
        /// 发布事件。
        /// </summary>
        void Publish(MobModifierEvent evt);

        /// <summary>
        /// 订阅事件。
        /// </summary>
        void Subscribe(Action<MobModifierEvent> handler);

        /// <summary>
        /// 取出当前待发送事件。
        /// </summary>
        IReadOnlyList<MobModifierEvent> Drain();

        /// <summary>
        /// 清空待发送事件与订阅。
        /// </summary>
        void Clear();
    }
}
