namespace DeadCellsMultiplayerX.Client.Session;

using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>
/// 表示一个 session (一局游戏)
/// 
/// 应该在游戏开始时初始化，不允许新玩家加入已开始的游戏
/// </summary>
public abstract class ClientSession : DisposableEventReceiver
{


    public virtual Task Init()
    {
        return Task.CompletedTask;
    }
}
