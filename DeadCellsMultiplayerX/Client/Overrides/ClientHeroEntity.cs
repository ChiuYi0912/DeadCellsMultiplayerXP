namespace DeadCellsMultiplayerX.Client.Overrides;

using dc.pr;
using DeadCellsMultiplayerX.Engine.Overrides;

/// <summary>
/// 客户端自定义 Beheaded 实体的扩展入口。
/// 客户端特有的原生行为在此接入；现有 RemoteHero 继续负责远程玩家表现。
/// </summary>
/// <remarks>
/// 当前不创建实例，也不接管输入、更新或动作回放。
/// 未来隐藏试运行前必须先隔离原生逻辑；visible=false 仅控制显示。
/// </remarks>
internal sealed class ClientHeroEntity : MultiplayerHero
{
    public ClientHeroEntity(Game game)
        : base(game)
    {
    }
}
