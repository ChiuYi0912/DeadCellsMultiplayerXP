namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc.en;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

internal sealed class RemoteHeroManager(
    GuestClientSession session,
    LocalHeroState localHero,
    string localGuid,
    ClientReplayProxyManager ghosts) : DisposableEventReceiver,
IOnLocalHeroInitDone
{
    private readonly GuestClientSession session = session;
    private readonly LocalHeroState localHero = localHero;
    private readonly string localGuid = localGuid;
    private readonly ClientReplayProxyManager ghosts = ghosts;

    /// <summary>
    /// 存储其他客户端玩家的实体
    /// </summary>
    private readonly Dictionary<string, RemoteHero> heroes = [];

#if DEBUG
    /// <summary>
    /// 用于调试其他客户端实体的生成
    /// </summary>
    private readonly Dictionary<string, string> mismatchLogged = [];

    /// <summary>
    /// 用于调试其他客户端实体的生成信息
    /// </summary>
    private string lastHeroSetSignature = string.Empty;
#endif
    /// <summary>
    /// 记录生成有误的玩家
    /// </summary>
    private readonly HashSet<string> missingSpawnLogged = [];

    /// <summary>
    /// 已应用到 ClientHeroReplayProxy 的 Loadout 版本
    /// </summary>
    private readonly Dictionary<string, uint> appliedLoadoutVersions = [];
    public IEnumerable<RemoteHero> All => this.heroes.Values;

    /// <summary>
    /// 当前客户端位于的关卡id
    /// </summary>
    private string LocalLevelId => this.localHero.Context.CurrentLevelId;

    /// <summary>
    /// 当前客户端位于的子关卡索引
    /// </summary>
    private int LocalSubLevel => this.localHero.Context.SubLevelIndex;

    /// <summary>
    /// 每应用一帧世界快照后做一次存在性对账
    /// </summary>
    /// <param name="frame">帧快照</param>
    /// <param name="frames">帧管理器</param>
    public void ApplyFrame(WorldFrame frame, WorldFrameTimeline frames)
    {
        if (frame == null)
        {
            return;
        }

#if DEBUG
        this.LogHeroSet(frame);
#endif

        // 静态外观变化时调用
        if (frame.Spawned != null)
        {
            foreach (var spawn in frame.Spawned)
            {
                if (spawn == null || spawn.GUID == this.localGuid)
                {
                    continue;
                }

                if (this.heroes.TryGetValue(spawn.GUID, out var existing)
                    && !existing.destroyed
                    && this.IsLocalLevel(existing))
                {
                    existing.ApplySpawn(spawn);
                }

                this.PushGhostLoadout(spawn);
            }
        }

        var seen = new HashSet<string>();
        if (frame.Heroes != null)
        {
            foreach (var state in frame.Heroes)
            {
                if (state == null || state.GUID == this.localGuid)
                {
                    continue;
                }

                //判断其他客户端是否与玩家在同一关卡,不在即销毁其他客户端实体
                if (!this.IsLocalLevel(state))
                {
#if DEBUG
                    this.LogMismatch(state);
#endif
                    this.Despawn(state.GUID);
                    continue;
                }

#if DEBUG
                this.mismatchLogged.Remove(state.GUID);
#endif
                seen.Add(state.GUID);

                this.heroes.TryGetValue(state.GUID, out var hero);

                // 本地关卡/子关卡已切换但实体仍是旧关卡创建的 先销毁再重建
                if (hero != null && !this.IsLocalLevel(hero))
                {
                    this.Despawn(state.GUID);
                    hero = null;
                }

                if (hero == null)
                {
                    if (!frames.TryGetSpawn(state.GUID, out var spawn))
                    {
                        if (this.missingSpawnLogged.Add(state.GUID))
                        {
                            this.Logger.Warning(
                                "No spawn for remote hero {Guid} on level {LevelId}#{SubLevelIndex}",
                                state.GUID, state.LevelId, state.SubLevelIndex);
                        }
                        continue;
                    }

                    hero = this.CreateHero(spawn, frames, state);
                    this.missingSpawnLogged.Remove(state.GUID);
                }

                var currentLevel = GuestClientSession.Game?.curLevel;
                if (currentLevel != null)
                {
                    this.ghosts.Ensure(state.GUID, currentLevel);
                }

                if (frames.TryGetSpawn(state.GUID, out var stateSpawn))
                {
                    this.PushGhostLoadout(stateSpawn);
                }

                hero.LevelId = state.LevelId;
                hero.SubLevelIndex = state.SubLevelIndex;
            }
        }

        // 服务端显式移除
        if (frame.Despawned != null)
        {
            foreach (var guid in frame.Despawned)
            {
                if (string.IsNullOrEmpty(guid) || guid == this.localGuid)
                {
                    continue;
                }

                this.Despawn(guid);
            }
        }

        // 不再出现在权威集合中的实体
        var toRemove = new List<string>();
        foreach (var guid in this.heroes.Keys)
        {
            if (!seen.Contains(guid))
            {
                toRemove.Add(guid);
            }
        }

        foreach (var guid in toRemove)
        {
            this.Despawn(guid);
        }
    }

    /// <summary>
    /// 销毁其中一位客户端实体
    /// </summary>
    /// <param name="guid">客户id</param>
    public void Despawn(string guid)
    {
        this.appliedLoadoutVersions.Remove(guid);

        if (this.heroes.Remove(guid, out var hero))
        {
            this.Logger.Information("Despawn remote hero {Guid}", guid);
            hero?.destroy();
        }

        // 可见 Hero 与隐形回放代理保持同步，避免双重清理。
        this.ghosts.Remove(guid);
    }

    public bool TryGet(string guid, out RemoteHero hero)
        => this.heroes.TryGetValue(guid, out hero!);

    protected override void MyDispose()
    {
        foreach (var h in this.heroes.Values)
        {
            h?.destroy();
        }

        this.heroes.Clear();
        this.appliedLoadoutVersions.Clear();
        base.MyDispose();
    }

    /// <summary>
    /// 把客户端静态数据中的完整装备应用到对应 ClientHeroReplayProxy
    /// </summary>
    private void PushGhostLoadout(HeroSpawn spawn)
    {
        if (spawn == null)
        {
            return;
        }

        if (this.appliedLoadoutVersions.TryGetValue(
                spawn.GUID, out var appliedVersion)
            && appliedVersion == spawn.Version)
        {
            return;
        }

        if (!this.ghosts.TryGet(spawn.GUID, out var ghost))
        {
            return;
        }

        ApplyLoadout(ghost, spawn);
        this.appliedLoadoutVersions[spawn.GUID] = spawn.Version;
    }

    private static void ApplyLoadout(
        ClientHeroReplayProxy ghost,
        HeroSpawn spawn)
    {
        if (spawn.Loadout != null)
        {
            ghost.Actions.ApplyLoadout(spawn.Loadout);
        }
        else
        {
            ghost.Actions.ApplyTalisman(spawn.Talisman);
        }
    }

    /// <summary>
    /// 创建客户实体
    /// </summary>
    /// <param name="spawn">客户玩家显示数据</param>
    /// <param name="frames">帧管理器</param>
    /// <param name="state">客户实体状态数据</param>
    /// <returns><see cref="RemoteHero"/></returns>
    private RemoteHero CreateHero(HeroSpawn spawn, WorldFrameTimeline frames, HeroDynamic state)
    {
        var game = GuestClientSession.Game;
        var level = game.curLevel;
        var hero = new RemoteHero(this.session, level, frames, spawn.GUID);
        hero.init();
        hero.ApplySpawn(spawn);
        this.heroes[spawn.GUID] = hero;

        var ghost = this.ghosts.Ensure(spawn.GUID, level);
        if (ghost != null)
        {
            ApplyLoadout(ghost, spawn);
            this.appliedLoadoutVersions[spawn.GUID] = spawn.Version;
        }

        // 直接用触发创建的新关卡状态摆放,避免用旧关卡插值坐标
        hero.SnapToState(state.LevelId, state.SubLevelIndex, state.Pos);

        var heroLevel = game.hero?._level;
        this.Logger.Information(
            "Created remote hero {Guid}: used={UsedLevel}#{UsedSub} state={StateLevel}#{StateSub} " +
            "cur={CurLevel}#{CurSub} heroLevel={HeroLevel}#{HeroSub} " +
            "statePos=({StateX},{StateY}) pos=({X},{Y}) destroyed={Destroyed}",
            spawn.GUID,
            LevelId(level), LevelIndex(level),
            state.LevelId, state.SubLevelIndex,
            LevelId(game.curLevel), LevelIndex(game.curLevel),
            LevelId(heroLevel), LevelIndex(heroLevel),
            state.Pos?.CX + state.Pos?.XR, state.Pos?.CY + state.Pos?.XY,
            hero.cx + hero.xr, hero.cy + hero.yr,
            hero.destroyed);

        return hero;
    }

    private static string LevelId(dc.pr.Level? level)
        => level?.map?.id?.ToString() ?? "<null>";

    private static int LevelIndex(dc.pr.Level? level)
        => level?.GetSubLevelIndex() ?? -1;


#if DEBUG
    private void LogMismatch(HeroDynamic state)
    {
        var key = $"{state.LevelId}#{state.SubLevelIndex}|{this.LocalLevelId}#{this.LocalSubLevel}";
        if (this.mismatchLogged.TryGetValue(state.GUID, out var last) && last == key)
        {
            return;
        }

        this.mismatchLogged[state.GUID] = key;
        this.Logger.Information(
            "Remote hero level mismatch: guid={Guid} state={StateLevel}#{StateSub} local={LocalLevel}#{LocalSub}",
            state.GUID, state.LevelId, state.SubLevelIndex, this.LocalLevelId, this.LocalSubLevel);
    }


    private void LogHeroSet(WorldFrame frame)
    {
        if (frame.Heroes == null)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var hero in frame.Heroes)
        {
            if (hero == null)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(';');
            }

            sb.Append(hero.GUID).Append('@')
              .Append(hero.LevelId).Append('#')
              .Append(hero.SubLevelIndex);
        }

        var signature = sb.ToString();
        if (signature == this.lastHeroSetSignature)
        {
            return;
        }

        this.lastHeroSetSignature = signature;
        this.Logger.Information("World frame heroes: {Heroes}", signature);
    }
#endif

    private bool IsLocalLevel(HeroDynamic state)
        => state.LevelId == this.LocalLevelId && state.SubLevelIndex == this.LocalSubLevel;

    private bool IsLocalLevel(RemoteHero hero)
        => hero.CreatedLevelId == this.LocalLevelId
        && hero.CreatedSubLevelIndex == this.LocalSubLevel;

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        if (!ReferenceEquals(hero, GuestClientSession.Game?.hero))
        {
            return;
        }

        // 关卡初始化.统一销毁旧关卡的所有 RemoteHero 与 Ghost
        // 下一帧从世界帧重新创建
        foreach (var h in this.heroes.Values)
        {
            h?.destroy();
        }

        this.heroes.Clear();
        this.missingSpawnLogged.Clear();
        this.appliedLoadoutVersions.Clear();
        this.ghosts.Clear();
    }
}
