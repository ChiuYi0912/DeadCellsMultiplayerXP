namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay;

using dc.pr;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Networking.Time;
using Serilog;

internal sealed class ClientReplayProxyManager(GuestGameSession session, string localGuid)
{
    private const long MAX_EVENT_AGE_MS = 2000;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientReplayProxyManager");
    private readonly Dictionary<HeroActionKind, IActionPresenter> presenters = ActionPresenterCatalog.CreateDefault();
    public string LocalGuid { get; } = localGuid;

    /// <summary>
    /// 可见远端 Hero 的只读查询入口，由组合根在创建后绑定。
    /// </summary>
    private RemoteHeroManager? remoteHeroes;

    /// <summary>
    /// 本地每个客户端对应的技能释放hero
    /// </summary>
    private readonly Dictionary<string, ClientHeroReplayProxy> ghosts = [];

    private readonly HeroOutcomeTimeline timeline = new();

    /// <summary>
    /// 已释放的结果
    /// </summary>
    private readonly HashSet<(string Caster, long OutcomeId)> appliedActions = [];

    private int lastEpoch = int.MinValue;

    public int Count => this.ghosts.Count;

    internal GuestGameSession Session { get; } = session;

    /// <summary>
    /// 绑定可见远端 Hero 查询；两者由组合根共同创建，存在互相引用。
    /// </summary>
    public void BindRemoteHeroes(RemoteHeroManager heroes) => this.remoteHeroes = heroes
            ?? throw new ArgumentNullException(nameof(heroes));

    public bool TryGetRemoteHero(string guid, out RemoteHero hero)
    {
        if (this.remoteHeroes != null)
        {
            return this.remoteHeroes.TryGet(guid, out hero);
        }

        hero = null!;
        return false;
    }

    /// <summary>
    /// 服务端完美格挡打断 mob 技能后，远端表现层停止该 mob 当前技能动画；
    /// 位置/affect 仍以随后的 EntityDynamic 为准。
    /// </summary>
    internal void InterruptRemoteMobSkill(uint netId)
    {
        if (netId == 0)
        {
            return;
        }

        var world = this.Session.ClientWorld;
        if (world?.TryGetRemoteMob(netId, out var mob) != true)
        {
            return;
        }

        var anim = mob.spr?.get_anim();
        if (anim != null && !anim.destroyed)
        {
            anim.stopWithStateAnims();
        }
    }

    /// <summary>
    /// 确保指定玩家在关卡中有 ClientHeroReplayProxy
    /// 仅由 RemoteHeroManager 在创建 RemoteHero 时调用
    /// </summary>
    /// <param name="guid">客户id</param>
    /// <param name="level">对应关卡</param>
    /// <returns></returns>
    public ClientHeroReplayProxy? Ensure(string guid, Level level)
    {
        if (string.IsNullOrEmpty(guid) || level == null)
        {
            return null;
        }

        if (this.ghosts.TryGetValue(guid, out var ghost))
        {
            if (ghost.MatchesLevel(level))
            {
                return ghost;
            }

            ghost.Dispose();
            this.ghosts.Remove(guid);
        }

        ghost = ClientHeroReplayProxy.CreateClient(GuestGameSession.Game, level, guid);
        if (ghost == null)
        {
            return null;
        }

        this.ghosts[guid] = ghost;
        return ghost;
    }

    public bool TryGet(string guid, out ClientHeroReplayProxy ghost)
        => this.ghosts.TryGetValue(guid, out ghost!);

    public void Remove(string guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        if (this.ghosts.Remove(guid, out var ghost))
        {
            ghost.Dispose();
        }
    }

    public void Clear()
    {
        foreach (var ghost in this.ghosts.Values)
        {
            ghost.Dispose();
        }

        this.ghosts.Clear();
        this.timeline.Clear();
    }

    public void Tick(long nowMs)
    {
        this.SyncPositions(GuestGameSession.Game?.curLevel);
        this.timeline.Drain(nowMs, this.Present);
    }

    public void SyncPositions(Level? level)
    {
        if (level == null || this.ghosts.Count == 0 || this.remoteHeroes == null)
        {
            return;
        }

        foreach (var (guid, ghost) in this.ghosts)
        {
            if (ghost == null
                || !ghost.IsAlive
                || !ghost.MatchesLevel(level))
            {
                continue;
            }

            if (!this.remoteHeroes.TryGet(guid, out var hero)
                || hero == null
                || hero.destroyed)
            {
                continue;
            }

            ghost.SetTransform(
                hero.cx, hero.cy, hero.xr, hero.yr, hero.dir,
                hero.dx, hero.dy);
        }
    }

    /// <summary>
    /// 应用事件结果
    /// </summary>
    /// <param name="frame">世界帧</param>
    /// <summary>
    /// 在帧应用前处理 epoch：先于 RemoteHeroManager 清理旧 Ghost。
    /// </summary>
    public void BeginFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        if (frame.Epoch == this.lastEpoch)
        {
            return;
        }

        this.Clear();
        this.appliedActions.Clear();
        this.lastEpoch = frame.Epoch;
    }

    public void ApplyFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        this.BeginFrame(frame);

        // 长时间运行后清理去重集合；旧事件本身有 2s 年龄过滤
        if (this.appliedActions.Count > 8192)
        {
            this.appliedActions.Clear();
        }

        if (frame.HeroOutcomes == null || frame.HeroOutcomes.Count == 0)
        {
            return;
        }

        foreach (var outcome in frame.HeroOutcomes)
        {
            if (outcome == null)
            {
                continue;
            }

            if (outcome.Epoch != frame.Epoch)
            {
                continue;
            }

            if (outcome.Status != HeroOutcomeStatus.Executed)
            {
                continue;
            }

            if (!this.appliedActions.Add((outcome.CasterGuid, outcome.OutcomeId)))
            {
                continue;
            }

            if (string.Equals(outcome.CasterGuid, this.LocalGuid, StringComparison.Ordinal))
            {
                continue;
            }

            if (frame.ServerTimeMs - outcome.ServerTimeMs > MAX_EVENT_AGE_MS)
            {
                continue;
            }

            if (outcome.Slot is < 0 or > 1)
            {
                continue;
            }

            this.timeline.Enqueue(
                frame,
                outcome,
                outcome.ServerTimeMs + SnapshotTiming.INTERPOLATION_DELAY_MS);
        }
    }

    private void Present(WorldFrame frame, HeroOutcome outcome)
    {
        var game = GuestGameSession.Game;
        var level = game?.curLevel;
        if (level == null)
        {
            return;
        }

        var levelId = level.map?.id?.ToString() ?? string.Empty;
        var subLevelIndex = level.GetSubLevelIndex();

        if (outcome.LevelId != levelId
            || outcome.SubLevelIndex != subLevelIndex)
        {
            return;
        }

        // ClientHeroReplayProxy 生命周期由 RemoteHeroManager 管理 这里只重放已存在的实例
        if (!this.TryGet(outcome.CasterGuid, out var ghost))
        {
            return;
        }

        if (!ghost.MatchesLevel(level))
        {
            this.Remove(outcome.CasterGuid);
            return;
        }

        ghost.SetTransform(
            outcome.CX, outcome.CY, outcome.XR, outcome.YR,
            outcome.Dir, outcome.DX, outcome.DY);
        ghost.SetTiers(
            outcome.BrutalityTier, outcome.SurvivalTier, outcome.TacticTier);

        if (!this.presenters.TryGetValue(outcome.Kind, out var presenter))
        {
            Logger.Debug(
                "No presenter for action kind {Kind}: caster={Caster}",
                outcome.Kind, outcome.CasterGuid);
            return;
        }

        presenter.Present(this, frame, outcome, ghost);
    }

    public void Dispose()
    {
        this.Clear();
        this.appliedActions.Clear();
        this.timeline.Clear();
    }
}
