namespace DeadCellsMultiplayerX.Server.Overrides;

using dc.pr;
using DeadCellsMultiplayerX.Engine.Overrides;

/// <summary>
/// 服务端自定义 Beheaded 实体的扩展入口，与当前关卡绑定。
/// 原生战斗行为在此适配；连接、命令校验和广播仍由服务端上层负责。
/// </summary>
/// <remarks>
/// 与 Heroes.ServerHero 的玩家聚合职责不同，当前不创建实例或接管战斗代理。
/// 未来启用时由 ServerWorld 内的玩家聚合持有，随关卡销毁。
/// </remarks>
internal sealed class ServerHeroEntity : MultiplayerHero
{
    public ServerHeroEntity(Game game)
        : base(game)
    {
    }

}
