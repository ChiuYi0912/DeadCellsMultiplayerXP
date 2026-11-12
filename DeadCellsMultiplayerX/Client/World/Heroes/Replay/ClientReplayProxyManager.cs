using dc.pr;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using Serilog;
using DeadCellsMultiplayerX.Client.Session.Guest;

using DeadCellsMultiplayerX.Engine.Utilities;
namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay
{
    internal sealed class ClientReplayProxyManager
    {
        private const long MAX_EVENT_AGE_MS = 2000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientReplayProxyManager");

        private readonly GuestClientSession session;
        private readonly Dictionary<HeroActionKind, IActionPresenter> presenters;
        public readonly string localGuid;

        /// <summary>
        /// 可见远端 Hero 的只读查询入口，由组合根在创建后绑定。
        /// </summary>
        private RemoteHeroManager? remoteHeroes;

        /// <summary>
        /// 本地每个客户端对应的技能释放hero
        /// </summary>
        private readonly Dictionary<string, ClientHeroReplayProxy> ghosts = [];

        /// <summary>
        /// 已释放的事件
        /// </summary>
        private readonly HashSet<(string Caster, long ActionId)> appliedActions = [];

        private int lastEpoch = int.MinValue;

        public ClientReplayProxyManager(GuestClientSession session, string localGuid)
        {
            this.session = session;
            this.localGuid = localGuid;
            presenters = ActionPresenterCatalog.CreateDefault();
        }

        public int Count => ghosts.Count;

        internal GuestClientSession Session => session;

        /// <summary>
        /// 绑定可见远端 Hero 查询；两者由组合根共同创建，存在互相引用。
        /// </summary>
        public void BindRemoteHeroes(RemoteHeroManager heroes)
        {
            remoteHeroes = heroes
                ?? throw new ArgumentNullException(nameof(heroes));
        }

        public bool TryGetRemoteHero(string guid, out RemoteHero hero)
        {
            if (remoteHeroes != null)
                return remoteHeroes.TryGet(guid, out hero);

            hero = null!;
            return false;
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
            if (string.IsNullOrEmpty(guid) || level == null) return null;

            if (ghosts.TryGetValue(guid, out var ghost))
            {
                if (ghost.MatchesLevel(level)) return ghost;

                ghost.Dispose();
                ghosts.Remove(guid);
            }

            ghost = ClientHeroReplayProxy.CreateClient(session.Game, level, guid);
            if (ghost == null) return null;

            ghosts[guid] = ghost;
            return ghost;
        }

        public bool TryGet(string guid, out ClientHeroReplayProxy ghost)
            => ghosts.TryGetValue(guid, out ghost!);

        public void Remove(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;

            if (ghosts.Remove(guid, out var ghost))
                ghost.Dispose();
        }

        public void Clear()
        {
            foreach (var ghost in ghosts.Values)
                ghost.Dispose();

            ghosts.Clear();
        }

        public void Tick(long nowMs)
        {
            SyncPositions(session?.Game?.curLevel);
        }

        public void SyncPositions(Level? level)
        {
            if (level == null || ghosts.Count == 0 || remoteHeroes == null)
                return;

            foreach (var (guid, ghost) in ghosts)
            {
                if (ghost == null
                    || !ghost.IsAlive
                    || !ghost.MatchesLevel(level))
                    continue;

                if (!remoteHeroes.TryGet(guid, out var hero)
                    || hero == null
                    || hero.destroyed)
                    continue;

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
            if (frame == null) return;

            if (frame.Epoch == lastEpoch) return;

            Clear();
            appliedActions.Clear();
            lastEpoch = frame.Epoch;
        }

        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            BeginFrame(frame);

            // 长时间运行后清理去重集合；旧事件本身有 2s 年龄过滤
            if (appliedActions.Count > 8192)
                appliedActions.Clear();

            if (frame.Actions == null || frame.Actions.Count == 0) return;

            var game = session.Game;
            var level = game?.curLevel;
            if (game == null || level == null) return;

            string levelId = level.map?.id?.ToString() ?? string.Empty;
            int subLevelIndex = level.GetSubLevelIndex();

            foreach (var evt in frame.Actions)
            {
                if (evt == null) continue;
                if (evt.Epoch != frame.Epoch) continue;
                if (!appliedActions.Add((evt.CasterGuid, evt.ActionId))) continue;
                if (string.Equals(evt.CasterGuid, localGuid, StringComparison.Ordinal)) continue;
                if (frame.ServerTimeMs - evt.ServerTimeMs > MAX_EVENT_AGE_MS) continue;
                if (evt.LevelId != levelId || evt.SubLevelIndex != subLevelIndex) continue;
                if (evt.Slot is < 0 or > 1) continue;

                // ClientHeroReplayProxy 生命周期由 RemoteHeroManager 管理 这里只重放已存在的实例
                if (!TryGet(evt.CasterGuid, out var ghost)) continue;

                if (!ghost.MatchesLevel(level))
                {
                    Remove(evt.CasterGuid);
                    continue;
                }

                ghost.SetTransform(
                    evt.CX, evt.CY, evt.XR, evt.YR, evt.Dir, evt.DX, evt.DY);
                ghost.SetTiers(
                    evt.BrutalityTier, evt.SurvivalTier, evt.TacticTier);

                if (!presenters.TryGetValue(evt.Kind, out var presenter))
                {
                    Logger.Debug(
                        "No presenter for action kind {Kind}: caster={Caster}",
                        evt.Kind, evt.CasterGuid);
                    continue;
                }

                presenter.Present(this, frame, evt, ghost);
            }
        }

        public void Dispose()
        {
            Clear();
            appliedActions.Clear();
        }
    }
}
