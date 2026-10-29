using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Client.Combat;
using DeadCellsMultiplayerX.Client.Heroes.LocalHero;
using DeadCellsMultiplayerX.Client.Heroes.Ghost;
using DeadCellsMultiplayerX.Client.Heroes.LocalHero;
using DeadCellsMultiplayerX.Client.Mobs;
using DeadCellsMultiplayerX.Client.Heroes.RemoteHero;
using DeadCellsMultiplayerX.Client.Effects;
using DeadCellsMultiplayerX.Client.Snapshot;
using DeadCellsMultiplayerX.Common.Core;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;

namespace DeadCellsMultiplayerX.Client.Replication
{
    /// <summary>
    /// 客户端世界层
    /// </summary>
    internal class RemoteWorld : DisposableEventReceiver
    {
        /// <summary>
        /// 客户端 session
        /// </summary>
        private readonly GuestClientSession session;

        /// <summary>
        /// 世界帧管理器，负责接收、应用和驱动世界帧
        /// </summary>
        private readonly WorldFrameManager frames;

        /// <summary>
        /// 远程英雄管理器，负责维护其他玩家的显示实体
        /// </summary>
        private readonly RemoteHeroManager heroes;

        /// <summary>
        /// 怪物同步装置，负责服务端怪物的生成、状态同步与销毁
        /// </summary>
        private readonly MobSyncManager mobs;

        /// <summary>
        /// 本地英雄状态
        /// </summary>
        private readonly LocalHeroState localHero;

        /// <summary>
        /// 远端玩家 GhostHero 生命周期与动作重放
        /// </summary>
        private readonly RemoteGhostManager ghosts;

        /// <summary>
        /// 服务端权威的动态关卡区域镜像
        /// </summary>
        private readonly LevelAreaAffectStateManager areaAffects;

        /// <summary>
        /// 本地玩家的唯一标识
        /// </summary>
        private readonly string localGuid;

        public RemoteWorld(GuestClientSession session, string localGuid)
        {
            this.session = session;
            this.localGuid = localGuid;
            frames = new WorldFrameManager(session, localGuid);
            mobs = new MobSyncManager(session, this, frames);
            heroes = new RemoteHeroManager(session, this);
            localHero = new LocalHeroState(session, this);
            ghosts = new RemoteGhostManager(session, this);
            areaAffects = new LevelAreaAffectStateManager(session);

            frames.FrameApplied += OnFrameApplied;
            frames.Start();
        }

        public LocalHeroState LocalHero => localHero;
        public RemoteHeroManager Heros => heroes;
        public RemoteGhostManager Ghosts => ghosts;
        public MobSyncManager Mobs => mobs;
        public LevelAreaAffectStateManager AreaAffects => areaAffects;
        public WorldFrameManager Frames => frames;
        public string LocalGuid => localGuid;
        public IEnumerable<RemoteHero> AllHeroes => heroes.All;

        public bool TryGetHero(string guid, out RemoteHero hero)
            => heroes.TryGet(guid, out hero);

        /// <summary>
        /// 处理接收到的世界帧
        /// </summary>
        /// <param name="frame">要处理的世界帧。</param>
        public void OnWorldFrame(WorldFrame frame) => frames.OnFrame(frame);

        /// <summary>
        /// 推进世界帧与怪物同步的定时逻辑
        /// </summary>
        /// <param name="nowMs">当前时间戳，单位为毫秒。</param>
        public void Tick(long nowMs)
        {
            frames.Tick(nowMs);
            mobs.Tick(nowMs);
        }

        /// <summary>
        /// 判断实体是否为服务端同步的怪物
        /// </summary>
        public bool IsRemoteMob(Mob mob) => mobs.IsRemoteMob(mob);

        public bool IsDeathSuppressed(Mob mob) => mobs.IsDeathSuppressed(mob);

        /// <summary>
        /// 在世界帧应用完成后，将帧数据应用到远程英雄与怪物
        /// </summary>
        /// <param name="frame">已应用的世界帧</param>
        private void OnFrameApplied(WorldFrame frame)
        {
            heroes.ApplyFrame(frame, frames);
            areaAffects.ApplyFrame(frame);
            ghosts.ApplyFrame(frame);
            mobs.ApplyFrame(frame);
            session.GhostHits.ApplyFrame(frame);
        }

        protected override void MyDispose()
        {
            frames.FrameApplied -= OnFrameApplied;
            mobs.Clear();
            areaAffects.Reset();
            ghosts.Dispose();
            heroes.Dispose();
            frames.Dispose();
            localHero.Dispose();
            base.MyDispose();
        }
    }
}
