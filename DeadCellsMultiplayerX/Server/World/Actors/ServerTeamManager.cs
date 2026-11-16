namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.pr;
using DeadCellsMultiplayerX.Server.Replication.Frames;

/// <summary>
/// 服务端 Hero 队伍同步入口：编排生成、状态镜像与战斗状态更新。
/// </summary>
internal sealed class ServerTeamManager(
    WorldState world,
    ClientFrameCollector frames,
    ServerHeroSpawner spawner,
    Func<Game?> gameProvider)
{
    /// <summary>
    /// 客户端停止上传状态多久后视为离线，关闭该 Ghost 的战斗状态
    /// </summary>
    private const long STALE_STATE_MS = 2000;

    private readonly WorldState world = world;

    /// <summary>
    /// 客户端上传的帧数据，提供每个 guid 的英雄状态
    /// </summary>
    private readonly ClientFrameCollector frames = frames;

    /// <summary>
    /// 获取当前 Game 实例
    /// </summary>
    private readonly Func<Game?> gameProvider = gameProvider;

    /// <summary>
    /// 当前可战斗 Hero
    /// </summary>
    public IReadOnlyCollection<ServerHero> Combatants
        => this.world.Actors;

    /// <summary>
    /// 用客户端最新上传状态同步所有 Hero
    /// </summary>
    public void Sync(IReadOnlyCollection<string> guests, long nowMs)
    {
        var game = this.gameProvider();
        if (game?.curLevel == null || game.hero == null)
        {
            return;
        }

        // 服务端自己的 Hero 没有玩家控制，不能参与 mob 仇恨
        this.NeutralizeServerHero(game);

        foreach (var guid in guests)
        {
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            if (!this.frames.TryGetHeroState(guid, out var state))
            {
                // 本地 Hero 死亡后客户端停止上传，服务端要把目标关闭
                if (this.world.Find(guid) is { } stale
                    && stale.LastStateMs > 0
                    && nowMs - stale.LastStateMs > STALE_STATE_MS)
                {
                    stale.DisableCombat();
                }

                continue;
            }

            if (state.Pos == null)
            {
                continue;
            }

            var hero = spawner.Ensure(game, guid, state, out var level);
            if (hero?.Ghost == null || level == null)
            {
                this.world.Find(guid)?.DisableCombat();
                continue;
            }

            var combat = ServerHeroStateMirror.Apply(hero.Ghost, state);
            hero.UpdateCombat(
                level,
                state.Life,
                state.MaxLife,
                state.IsDead,
                combat,
                nowMs);
        }
    }

    /// <summary>
    /// 按 GUID 获取 Hero
    /// </summary>
    public bool TryGet(string guid, out ServerHero hero)
    {
        var found = this.world.Find(guid);
        if (found != null)
        {
            hero = found;
            return true;
        }

        hero = null!;
        return false;
    }

    /// <summary>
    /// 通过底层 Hero 反查
    /// </summary>
    public bool TryGet(Entity entity, out ServerHero hero)
    {
        var found = this.world.FindByHero(entity);
        if (found != null)
        {
            hero = found;
            return true;
        }

        hero = null!;
        return false;
    }

    /// <summary>
    /// 断线清理
    /// </summary>
    public void Forget(string guid)
        => this.world.Find(guid)?.DisableCombat();

    /// <summary>
    /// 世界重置
    /// </summary>
    public void Clear()
    {
        foreach (var hero in this.world.Actors)
        {
            hero.DisableCombat();
        }
    }

    /// <summary>
    /// 将服务端的 Hero 设置为不可攻击状态。
    /// </summary>
    private void NeutralizeServerHero(Game game)
    {
        var hero = game?.hero;
        if (hero == null || hero.destroyed)
        {
            return;
        }

        if (this.world.FindByHero(hero) != null)
        {
            return;
        }

        if (hero._targetable)
        {
            hero.set_targetable(false);
        }

        if (!hero.ignoreDetection)
        {
            hero.ignoreDetection = true;
        }
    }
}
