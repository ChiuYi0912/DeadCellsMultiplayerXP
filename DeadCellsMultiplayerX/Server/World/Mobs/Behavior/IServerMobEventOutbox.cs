namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior;

using DeadCellsMultiplayerX.Contracts.MobBehavior;

/// <summary>
/// 服务端 mob 修改器出站事件队列。
/// 事件由世界帧读取并在成功后消费，不提供进程内订阅。
/// </summary>
public interface IServerMobEventOutbox
{
    /// <summary>
    /// 入队一个待发送事件。
    /// </summary>
    public void Enqueue(MobModifierEvent evt);

    /// <summary>
    /// 读取当前待发送事件，不消费队列。
    /// </summary>
    public IReadOnlyList<MobModifierEvent> Peek();

    /// <summary>消费成功构建帧包含的事件。</summary>
    public void Commit(int count);

    /// <summary>
    /// 清空待发送事件。
    /// </summary>
    public void Clear();
}
