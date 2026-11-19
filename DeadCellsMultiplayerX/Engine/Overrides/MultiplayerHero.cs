namespace DeadCellsMultiplayerX.Engine.Overrides;

using dc.en.hero;
using dc.pr;

/// <summary>
/// 多人 Hero 原生实体的共同基类，仅承载两端通用的原生行为修复与扩展
/// 网络会话、客户端表现和服务端权威规则由各自模块负责
/// </summary>
/// <remarks>
/// 构造会执行 Beheaded 的原生构造与管理器创建；本类型不提供隔离或暂停保证
/// </remarks>
internal abstract class MultiplayerHero(Game game) : Beheaded(game ?? throw new ArgumentNullException(nameof(game)))
{

}
