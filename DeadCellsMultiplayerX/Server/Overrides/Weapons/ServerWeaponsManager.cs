namespace DeadCellsMultiplayerX.Server.Overrides.Weapons;

using dc.en;
using DeadCellsMultiplayerX.Engine.Overrides.Weapons;

/// <summary>服务端武器管理器原生行为的重写入口。</summary>
internal sealed class ServerWeaponsManager(Hero hero) : MultiplayerWeaponsManager(hero)
{
}
