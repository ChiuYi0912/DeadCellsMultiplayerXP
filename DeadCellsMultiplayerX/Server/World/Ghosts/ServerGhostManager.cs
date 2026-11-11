using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.pr;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Diagnostics;
using DeadCellsMultiplayerX.Common.EntityEvents;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Server.World.Ghosts.Aggro;
using DeadCellsMultiplayerX.Server.World.Ghosts.Pipeline;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
using DeadCellsMultiplayerX.Server.World.Ghosts.World;
using DeadCellsMultiplayerX.Server.MobAI.Combat;
using DeadCellsMultiplayerX.Server.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Modifiers;
using DeadCellsMultiplayerX.Utils;
using Serilog;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.World.Replication.Frames;

namespace DeadCellsMultiplayerX.Server.World.Ghosts
{
    /// <summary>
    /// 服务端 Ghost 入口。
    /// 思路：聚合世界状态、队伍、仇恨、mob 事件与动作管线；
    /// 对外暴露 ActionJournal 等诊断入口，动作执行细节下沉到 ActionPipeline/Modules。
    /// </summary>
    internal sealed class ServerGhostManager : IServerEntityEventSource
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        private readonly ServerSession session;
        private readonly WorldState world;
        private readonly SpawnSynchronizer spawns;
        private readonly ActionPipeline pipeline;
        private readonly ServerTeamManager teams;
        private readonly ServerAggroManager aggro;
        private readonly ServerMobEventBus mobEvents;
        private readonly ServerMobModifierRegistry mobModifiers;
        private readonly ServerMobAttackRouter mobAttacks;
        private readonly ActionJournal actionJournal = new();

        public ServerGhostManager(ServerSession session)
        {
            this.session = session;

            world = new WorldState();
            spawns = new SpawnSynchronizer(session.ClientFrames);
            teams = new ServerTeamManager(
                world,
                session.ClientFrames,
                spawns,
                () => Game.Class.ME);
            aggro = new ServerAggroManager(
                teams,
                session.Entities,
                () => Game.Class.ME);
            pipeline = ActionPipeline.CreateDefault(
                world,
                spawns,
                session.ClientFrames,
                session.Entities,
                session.EntityPublisher.History,
                () => session.CurrentTimeStamp,
                () => Game.Class.ME,
                actionJournal);

            mobEvents = new ServerMobEventBus();
            mobModifiers = new ServerMobModifierRegistry(
                session, this, teams, mobEvents);
            mobAttacks = new ServerMobAttackRouter(events: mobEvents);
        }

        /// <summary>
        /// 服务端 Ghost 队伍管理器
        /// </summary>
        public ServerTeamManager Team => teams;

        /// <summary>
        /// 服务端 Ghost 仇恨协调器
        /// </summary>
        public ServerAggroManager Aggro => aggro;

        /// <summary>
        /// 服务端 mob 修改器注册器
        /// </summary>
        public ServerMobModifierRegistry MobModifiers => mobModifiers;

        /// <summary>
        /// 服务端 mob 事件总线
        /// </summary>
        public IServerMobEventBus MobEvents => mobEvents;

        /// <summary>
        /// 服务端 mob 攻击释放路由
        /// </summary>
        public IServerMobAttackRouter MobAttacks => mobAttacks;

        /// <summary>
        /// 英雄动作诊断日志与计数
        /// </summary>
        public ActionJournal ActionJournal => actionJournal;

        /// <summary>
        /// 实际持有 ServerGhostHero 的数量
        /// </summary>
        public int Count => world.Count;

        /// <summary>
        /// 每个游戏 tick 同步 Ghost 位置/队伍并推进仇恨协调
        /// </summary>
        public void Tick(double dt, long nowMs, IReadOnlyCollection<string> guests)
        {
            teams.Sync(guests, nowMs);
            aggro.Tick(nowMs);
            mobModifiers.Tick(nowMs);
            mobAttacks.Tick(nowMs);
        }

        /// <summary>
        /// 通过底层 Hero 查找可战斗 Ghost
        /// </summary>
        public bool TryGetCombatant(Entity entity, out GhostCombatant combatant)
            => teams.TryGet(entity, out combatant);

        /// <summary>
        /// 世界重置,写入新 epoch 并清空 Ghost 状态
        /// </summary>
        public void BeginEpoch(int value)
        {
            world.BeginEpoch(value);
            teams.Clear();

            mobEvents.Clear();
            mobAttacks.Clear();

            Logger.Information(
                "Ghost action epoch begin: {Epoch}", value);
        }

        /// <summary>
        /// 清理 ServerGhostHero 与待广播事件
        /// </summary>
        public void Clear()
        {
            world.Reset();
            teams.Clear();
            mobModifiers.Clear();
            mobEvents.Clear();
            mobAttacks.Clear();
        }

        /// <summary>
        /// 客户端断线时清理它的 Ghost 与准入状态
        /// </summary>
        public void RemoveGuest(string guid)
        {
            teams.Forget(guid);
            world.RemoveGuest(guid);
        }

        /// <summary>
        /// 处理客户端上报的英雄动作
        /// </summary>
        public void HandleAction(string senderGuid, HeroAction? action)
            => pipeline.Handle(senderGuid, action);

        /// <summary>
        /// 用客户端最新 spawn 同步所有 ServerGhostHero 的项链
        /// </summary>
        public void SyncTalismans() => spawns.SyncAll(world.Actors);

        /// <summary>
        /// 取出本帧待广播的动作事件
        /// </summary>
        public List<HeroActionEvent> DrainActions() => world.DrainEvents();

        /// <summary>
        /// 取出 mob 修改器事件；未来由 WorldFrame 广播
        /// </summary>
        public IReadOnlyList<MobModifierEvent> DrainMobEvents()
            => mobEvents.Drain();

        /// <inheritdoc />
        public IReadOnlyList<EntityEvent> DrainEntityEvents()
            => mobEvents.Drain();
    }
}
