using DeadCellsMultiplayerX.Contracts.MobBehavior;
namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior
{
    /// <summary>
    /// 服务端 mob 修改器出站事件队列。
    /// 事件由世界帧统一 Drain 后广播，不提供进程内订阅。
    /// </summary>
    public interface IServerMobEventQueue
    {
        /// <summary>
        /// 入队一个待发送事件。
        /// </summary>
        void Enqueue(MobModifierEvent evt);

        /// <summary>
        /// 取出当前待发送事件。
        /// </summary>
        IReadOnlyList<MobModifierEvent> Drain();

        /// <summary>
        /// 清空待发送事件。
        /// </summary>
        void Clear();
    }
}
