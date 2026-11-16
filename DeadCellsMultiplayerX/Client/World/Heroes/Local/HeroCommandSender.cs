namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 本地 HeroCommand 的入队入口；实际发送由 ClientWorldFrame 附带完成。
/// </summary>
internal sealed class HeroCommandSender(HeroCommandQueue queue)
{
    private readonly HeroCommandQueue queue = queue
            ?? throw new ArgumentNullException(nameof(queue));

    public long NextCommandId() => this.queue.NextCommandId();

    public void Send(HeroCommand command) => this.queue.Enqueue(command);
}
