using System.Collections.Generic;
using System.Linq;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Server.Connection;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Hubs
{
    /// <summary>
    /// 服务端世界帧广播器
    /// 每个 tick 组装一帧完整世界状态
    /// 写入历史环并广播给所有客户端
    /// 同时为客户端补发请求提供历史帧和全量基线
    /// </summary>
    internal sealed class ServerWorldBroadcaster
    {
        /// <summary>
        /// 发送间隔秒数 对应 20Hz
        /// </summary>
        private const double SendIntervalSeconds = 0.05;

        /// <summary>
        /// 广播历史帧上限
        /// </summary>
        private const int MaxHistory = 128;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "WorldFrameBroadcaster");

        /// <summary>
        /// 世界帧收集器
        /// </summary>
        private readonly ClientWorldFrameCollector collector;

        /// <summary>
        /// 怪物状态发布器
        /// </summary>
        private readonly ServerMobPublisher mobPublisher;

        /// <summary>
        /// 本帧待广播的英雄动作
        /// </summary>
        private readonly ServerGhostManager ghosts;

        /// <summary>
        /// 服务端动态关卡区域
        /// </summary>
        private readonly ServerLevelAreaAffectRegistry areaAffects;

        /// <summary>
        /// 广播历史帧
        /// </summary>
        private readonly List<WorldFrame> history = [];

        /// <summary>
        /// 发送时间累加器
        /// </summary>
        private double accumulator;

        /// <summary>
        /// 创建世界帧广播器
        /// </summary>
        public ServerWorldBroadcaster(
            ClientWorldFrameCollector collector,
            ServerMobPublisher mobPublisher,
            ServerGhostManager ghosts,
            ServerLevelAreaAffectRegistry areaAffects)
        {
            this.collector = collector;
            this.mobPublisher = mobPublisher;
            this.ghosts = ghosts;
            this.areaAffects = areaAffects;
        }

        /// <summary>
        /// 当前世界 epoch
        /// </summary>
        public int Epoch { get; private set; }

        /// <summary>
        /// 当前广播序号
        /// </summary>
        public long Sequence { get; private set; } = -1;

        /// <summary>
        /// 已广播帧数
        /// </summary>
        public int BroadcastFrames { get; private set; }

        /// <summary>
        /// 补发请求服务次数
        /// </summary>
        public int BundleServed { get; private set; }

        /// <summary>
        /// 重同步服务次数
        /// </summary>
        public int ResyncServed { get; private set; }

        /// <summary>
        /// 世界重置 递增 epoch 序号归零 清空历史与收集器旧世界状态
        /// </summary>
        public void BeginEpoch(long serverTimeMs)
        {
            Epoch++;
            Sequence = -1;
            accumulator = 0;
            history.Clear();
            collector.BeginEpoch(Epoch);
            Logger.Information("World epoch begin: epoch={Epoch}", Epoch);
        }

        /// <summary>
        /// 按发送间隔推进广播
        /// </summary>
        public void Tick(double dt, long serverTimeMs, IReadOnlyList<SGuestConnection> guests)
        {
            accumulator += dt;
            if (accumulator < SendIntervalSeconds) return;
            accumulator -= SendIntervalSeconds;

            Emit(serverTimeMs, guests);
        }

        /// <summary>
        /// 按序号范围获取历史帧
        /// </summary>
        public WorldFrameBundle GetFrames(WorldFrameRangeRequest request, long serverTimeMs)
        {
            if (request == null
                || request.ToSequence < request.FromSequence
                || history.Count == 0)
            {
                ResyncServed++;
                return new WorldFrameBundle { Resync = BuildResync(serverTimeMs) };
            }

            long first = history[0].Sequence;
            long last = history[^1].Sequence;

            if (request.FromSequence < first || request.ToSequence > last)
            {
                ResyncServed++;
                var available = history
                    .Where(f => f.Sequence >= request.FromSequence && f.Sequence <= request.ToSequence)
                    .ToList();
                return new WorldFrameBundle
                {
                    Frames = available,
                    Resync = BuildResync(serverTimeMs),
                };
            }

            BundleServed++;
            int start = (int)(request.FromSequence - first);
            int count = (int)(request.ToSequence - request.FromSequence + 1);
            return new WorldFrameBundle { Frames = history.GetRange(start, count) };
        }

        /// <summary>
        /// 构建全量世界基线
        /// </summary>
        public WorldResync BuildResync(long serverTimeMs)
        {
            var mobs = mobPublisher.BuildResync(serverTimeMs);

            return new WorldResync
            {
                Epoch = Epoch,
                Sequence = Sequence,
                ServerTimeMs = serverTimeMs,
                Heroes = collector.GetActiveHeroDynamics().ToList(),
                Spawns = collector.GetAllSpawns(),
                Mobs = mobs.Mobs,
                MobSpawns = mobs.Spawned,
                AreaAffects = areaAffects.Collect(serverTimeMs),
            };
        }

        /// <summary>
        /// 获取最新广播帧
        /// </summary>
        public WorldFrame? GetLatest()
            => history.Count > 0 ? history[^1] : null;

        public void Clear()
        {
            history.Clear();
            accumulator = 0;
        }

        /// <summary>
        /// 组装并广播一帧世界状态
        /// </summary>
        private void Emit(long serverTimeMs, IReadOnlyList<SGuestConnection> guests)
        {
            Sequence++;
            BroadcastFrames++;

            ghosts.SyncTalismans();

            var mobs = mobPublisher.Collect(serverTimeMs);

            var frame = new WorldFrame
            {
                Epoch = Epoch,
                Sequence = Sequence,
                ServerTimeMs = serverTimeMs,
                Heroes = collector.GetActiveHeroDynamics().ToList(),
                Spawned = collector.DrainSpawnChanges(),
                Despawned = collector.DrainRemoved(),
                Mobs = mobs.Mobs,
                SpawnedMobs = mobs.Spawned,
                DespawnedMobs = mobs.Despawned,
                Actions = ghosts.DrainActions(),
                AreaAffects = areaAffects.Collect(serverTimeMs),
                GhostHits = ghosts.DrainHits(),
            };

            if (BroadcastFrames == 1)
            {
                Logger.Information(
                    "First world frame emitted: epoch={Epoch} seq={Sequence} heroes={Heroes}",
                    Epoch, Sequence, frame.Heroes.Count);
            }

            history.Add(frame);
            if (history.Count > MaxHistory)
                history.RemoveRange(0, history.Count - MaxHistory);

            for (int i = 0; i < guests.Count; i++)
                guests[i].PushWorldFrame(frame);
        }
    }
}
