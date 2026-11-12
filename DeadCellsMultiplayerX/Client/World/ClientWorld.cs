using dc.en;
using DeadCellsMultiplayerX.Client.Presentation.Effects;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Client.World.Mobs.Replication;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

namespace DeadCellsMultiplayerX.Client.World
{
    /// <summary>
    /// 客户端世界组合根。
    /// 负责创建并拥有子系统，对外只暴露帧路由、Tick 与少量只读查询；
    /// 子系统之间通过构造参数获得各自的窄依赖，不再接收整个世界。
    /// </summary>
    internal class ClientWorld : DisposableEventReceiver
    {
        /// <summary>
        /// 世界帧管理器，负责接收、应用和驱动世界帧
        /// </summary>
        private readonly WorldFrameTimeline frames;

        /// <summary>
        /// 远程英雄管理器，负责维护其他玩家的显示实体
        /// </summary>
        private readonly RemoteHeroManager heroes;

        /// <summary>
        /// 通用实体存储
        /// </summary>
        private readonly ClientEntityStore entityStore;

        /// <summary>
        /// 通用实体帧管线
        /// </summary>
        private readonly ClientEntityFrameApplier entities;

        /// <summary>
        /// Mob 同步装置，负责服务端怪物的生成、状态同步与销毁
        /// </summary>
        private readonly ClientMobReplicator mobs;

        /// <summary>
        /// 本地英雄状态
        /// </summary>
        private readonly LocalHeroState localHero;

        /// <summary>
        /// 远端玩家 ClientHeroReplayProxy 生命周期与动作重放
        /// </summary>
        private readonly ClientReplayProxyManager ghosts;

        /// <summary>
        /// 服务端权威的动态关卡区域镜像
        /// </summary>
        private readonly ClientAreaAffectState areaAffects;

        /// <summary>
        /// 客户端 mob 修改器注册器
        /// </summary>
        private readonly ClientMobModifierRegistry mobModifiers;

        public ClientWorld(GuestClientSession session, string localGuid)
        {
            frames = new WorldFrameTimeline(session, localGuid);
            entityStore = new ClientEntityStore();

            // Ghost 代理与可见 Hero 互相引用；先创建代理，再创建 Hero 后绑定查询。
            ghosts = new ClientReplayProxyManager(session, localGuid);
            mobModifiers = new ClientMobModifierRegistry(
                session, entityStore, ghosts);
            mobs = new ClientMobReplicator(
                session, mobModifiers, frames, entityStore);
            entityStore.RegisterHandler(mobs);

            entities = new ClientEntityFrameApplier(entityStore);
            entities.DebugLabels.Register(new MobDebugTextProvider(session));

            localHero = new LocalHeroState(session);
            heroes = new RemoteHeroManager(
                session, localHero, localGuid, ghosts);
            ghosts.BindRemoteHeroes(heroes);

            areaAffects = new ClientAreaAffectState(session);

            frames.FrameApplied += OnFrameApplied;
            frames.Start();
        }

        /// <summary>
        /// 处理接收到的世界帧
        /// </summary>
        public void OnWorldFrame(WorldFrame frame) => frames.OnFrame(frame);

        /// <summary>
        /// 推进世界帧与怪物同步的定时逻辑
        /// </summary>
        public void Tick(long nowMs)
        {
            ghosts.Tick(nowMs);
            frames.Tick(nowMs);
            entities.Tick(nowMs);
        }

        /// <summary>
        /// 当前世界帧 epoch
        /// </summary>
        internal int Epoch => frames.Epoch;

        /// <summary>
        /// 客户端 mob 修改器入口
        /// </summary>
        internal ClientMobModifierRegistry MobModifiers => mobModifiers;

        /// <summary>
        /// 当前是否正在应用服务端区域效果
        /// </summary>
        internal bool IsApplyingServerAreaAffect => areaAffects.IsApplyingServer;

        /// <summary>
        /// 判断实体是否为服务端同步的怪物
        /// </summary>
        internal bool IsRemoteMob(Mob mob) => mobs.IsRemoteMob(mob);

        internal bool IsDeathSuppressed(Mob mob) => mobs.IsDeathSuppressed(mob);

        /// <summary>
        /// 查询原生 mob 对应的 NetId
        /// </summary>
        internal bool TryGetMobNetId(Mob mob, out uint netId)
            => mobs.Receiver.TryGetNetId(mob, out netId);

        /// <summary>
        /// 采集调试 UI 所需的只读快照
        /// </summary>
        internal ClientWorldStats CaptureStats()
        {
            int mobCount = 0;
            foreach (var mob in mobs.Receiver.All)
            {
                if (!mob.destroyed)
                    mobCount++;
            }

            int heroCount = 0;
            foreach (var _ in heroes.All)
                heroCount++;

            int entityCount = 0;
            int pendingSpawns = 0;
            foreach (var record in entityStore.Records)
            {
                entityCount++;
                if (!record.Created)
                    pendingSpawns++;
            }

            var local = localHero.Context;
            return new ClientWorldStats(
                frames.Epoch,
                frames.LastFrameAppliedAtMs,
                frames.AppliedFramesPerSecond,
                frames.HasBaseline,
                frames.LastDeliveredSequence,
                frames.PendingCount,
                frames.BufferedFrames,
                frames.GapCount,
                frames.NackSent,
                frames.BundleReceived,
                frames.ResyncCount,
                frames.SkippedFrames,
                frames.DroppedOld,
                frames.PendingOverflow,
                mobCount,
                ghosts.Count,
                heroCount,
                entityCount,
                pendingSpawns,
                local.CurrentLevelId,
                local.SubLevelIndex,
                local.HasHero,
                local.IsAlive);
        }

        /// <summary>
        /// 在世界帧应用完成后，将帧数据应用到远程英雄与怪物
        /// </summary>
        private void OnFrameApplied(WorldFrame frame)
        {
            ghosts.BeginFrame(frame);
            heroes.ApplyFrame(frame, frames);
            areaAffects.ApplyFrame(frame);
            ghosts.ApplyFrame(frame);
            entities.ApplyFrame(frame);
        }

        protected override void MyDispose()
        {
            frames.FrameApplied -= OnFrameApplied;
            mobModifiers.Clear();
            entities.Clear();
            areaAffects.Reset();
            heroes.Dispose();
            ghosts.Dispose();
            frames.Dispose();
            localHero.Dispose();
            base.MyDispose();
        }
    }
}
