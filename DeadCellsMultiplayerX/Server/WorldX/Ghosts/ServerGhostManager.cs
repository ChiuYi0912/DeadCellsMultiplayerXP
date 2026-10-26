using System;
using System.Collections.Generic;
using dc;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.Pipeline;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.World;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts
{
    /// <summary>
    /// 服务端 Ghost 入口
    /// </summary>
    internal sealed class ServerGhostManager
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        private readonly WorldState world;
        private readonly SpawnSynchronizer spawns;
        private readonly ActionPipeline pipeline;

        public ServerGhostManager(ServerSession session)
        {
            world = new WorldState();
            spawns = new SpawnSynchronizer(session.ClientFrames);
            pipeline = ActionPipeline.CreateDefault(
                world,
                spawns,
                session.ClientFrames,
                () => session.CurrentTimeStamp,
                () => Game.Class.ME);
        }

        /// <summary>
        /// 实际持有 GhostHero 的数量
        /// </summary>
        public int Count => world.Count;

        /// <summary>
        /// 世界重置,写入新 epoch 并清空 Ghost 状态
        /// </summary>
        public void BeginEpoch(int value)
        {
            world.BeginEpoch(value);

            Logger.Information(
                "Ghost action epoch begin: {Epoch}", value);
        }

        /// <summary>
        /// 清理 GhostHero 与待广播事件
        /// </summary>
        public void Clear() => world.Reset();

        /// <summary>
        /// 客户端断线时清理它的 Ghost 与准入状态
        /// </summary>
        public void RemoveGuest(string guid) => world.RemoveGuest(guid);

        /// <summary>
        /// 处理客户端上报的英雄动作
        /// </summary>
        public void HandleAction(string senderGuid, HeroAction? action)
            => pipeline.Handle(senderGuid, action);

        /// <summary>
        /// 用客户端最新 spawn 同步所有 GhostHero 的项链
        /// </summary>
        public void SyncTalismans() => spawns.SyncAll(world.Actors);

        /// <summary>
        /// 取出本帧待广播的动作事件
        /// </summary>
        public List<HeroActionEvent> DrainActions() => world.DrainEvents();
    }
}
