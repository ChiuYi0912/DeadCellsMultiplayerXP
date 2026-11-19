namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;

/// <summary>
/// 根据客户端上传状态解析关卡并确保 ServerHeroCombatProxy 存在。
/// </summary>
internal sealed class ServerHeroSpawner(
    ServerActorState actorState,
    SpawnSynchronizer spawns)
{
    private readonly ServerActorState actorState = actorState
            ?? throw new ArgumentNullException(nameof(actorState));
    private readonly SpawnSynchronizer spawns = spawns
            ?? throw new ArgumentNullException(nameof(spawns));

    /// <summary>
    /// 确保 guid 对应的 Ghost 在状态所在关卡存在；返回 null 表示当前不可创建。
    /// </summary>
    public ServerHero? Ensure(
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

        var hero = this.actorState.GetOrCreate(guid);
        var created = hero.EnsureGhost(
            game, level, state.Pos.CX, state.Pos.CY);

        if (hero.Ghost == null)
        {
            return null;
        }

        if (created)
        {
            this.spawns.Sync(hero);
        }

        return hero;
    }
}
