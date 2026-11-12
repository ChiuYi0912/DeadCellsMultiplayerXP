namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;

/// <summary>
/// 根据客户端上传状态解析关卡并确保 ServerHeroCombatProxy 存在。
/// </summary>
internal sealed class ServerHeroSpawner(
    WorldState world,
    SpawnSynchronizer spawns,
    Func<Game?> gameProvider)
{
    private readonly WorldState world = world
            ?? throw new ArgumentNullException(nameof(world));
    private readonly SpawnSynchronizer spawns = spawns
            ?? throw new ArgumentNullException(nameof(spawns));
    private readonly Func<Game?> gameProvider = gameProvider
            ?? throw new ArgumentNullException(nameof(gameProvider));

    /// <summary>
    /// 确保 guid 对应的 Ghost 在状态所在关卡存在；返回 null 表示当前不可创建。
    /// </summary>
    public Actor? Ensure(
        Game game, string guid, HeroDynamic state, out Level? level)
    {
        level = null;

        if (game == null || state?.Pos == null)
        {
            return null;
        }

        level = LevelUtils.ResolveLevel(
            game, state.LevelId, state.SubLevelIndex);
        if (level == null)
        {
            return null;
        }

        var actor = this.world.GetOrCreate(guid);
        var created = actor.EnsureGhost(
            game, level, state.Pos.CX, state.Pos.CY);

        if (actor.Ghost == null)
        {
            return null;
        }

        if (created)
        {
            this.spawns.Sync(actor);
        }

        return actor;
    }
}
