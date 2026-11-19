namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;
using DeadCellsMultiplayerX.Server.World.Heroes.Shield;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using Serilog;

/// <summary>
/// 服务端 Ghost 入口。
/// 思路：聚合世界状态、队伍、仇恨、mob 事件与动作分发；
/// 对外暴露 HeroCommandJournal 等诊断入口，动作执行细节下沉到 HeroCommandDispatcher/Modules。
/// </summary>
internal sealed class ServerActorManager : IServerEntityEventSource
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

    private readonly WorldState world;
    private readonly SpawnSynchronizer spawns;
    private readonly HeroCommandDispatcher dispatcher;
    private readonly ServerTeamManager teams;
    private readonly ServerMobEventOutbox mobEvents;
    private readonly ServerMobAttackRouter mobAttacks;

    public ServerActorManager(ClientFrameCollector frames, ServerEntityRegistry entities,
        ServerEntityHistory history, Func<long> getTime, Func<int> getEpoch)
    {
        this.world = new WorldState();
        this.spawns = new SpawnSynchronizer(frames);
        var spawner = new ServerHeroSpawner(
            this.world, this.spawns, () => Game.Class.ME);
        this.Shield = new ShieldAttackArbiter(
            this.world, entities, getTime);
        this.teams = new ServerTeamManager(
            this.world,
            frames,
            spawner,
            () => Game.Class.ME);
        this.Aggro = new ServerAggroManager(
            this.teams,
            entities,
            () => Game.Class.ME);
        this.dispatcher = new HeroCommandDispatcher(
            this.world,
            spawner,
            this.spawns,
            frames,
            entities,
            history,
            getTime,
            () => Game.Class.ME);

        this.mobEvents = new ServerMobEventOutbox();
        this.mobAttacks = new ServerMobAttackRouter(events: this.mobEvents);
        var mobModifiers = new ServerMobModifierRegistry(
            entities, getEpoch, getTime, this.teams, this.mobAttacks, this.mobEvents);
        this.Mobs = new ServerMobRuntime(
            mobModifiers, this.Aggro, getTime);
    }

    /// <summary>
    /// 服务端 Ghost 仇恨协调器
    /// </summary>
    public ServerAggroManager Aggro { get; }

    /// <summary>
    /// 服务端 mob 运行时入口
    /// </summary>
    public ServerMobRuntime Mobs { get; }

    /// <summary>
    /// 盾牌原生格挡/反击观测器。
    /// </summary>
    public ShieldAttackArbiter Shield { get; }

    /// <summary>
    /// 动作处理日志，供调试命令导出。
    /// </summary>
    public HeroCommandJournal Journal => this.dispatcher.Journal;

    /// <summary>
    /// 每个游戏 tick 同步 Ghost 位置/队伍并推进仇恨协调
    /// </summary>
    public void Tick(long nowMs, IReadOnlyCollection<string> guests)
    {
        this.teams.Sync(guests, nowMs);
        this.Aggro.Tick(nowMs);
        this.Mobs.Tick(nowMs);
        this.mobAttacks.Tick(nowMs);
    }

    /// <summary>
    /// 查找可战斗 Ghost
    /// </summary>
    public bool TryGetCombatant(Entity entity, out ServerHero hero)
        => this.teams.TryGet(entity, out hero);

    /// <summary>
    /// 世界重置
    /// </summary>
    public void BeginEpoch(int value)
    {
        this.world.BeginEpoch(value);
        this.teams.Clear();
        this.dispatcher.BeginEpoch();
        this.Shield.Clear();

        this.Mobs.Clear();
        this.mobEvents.Clear();
        this.mobAttacks.Clear();

        Logger.Information("Ghost action epoch begin: {Epoch}", value);
    }

    /// <summary>
    /// 清理 ServerHeroCombatProxy 与待广播事件
    /// </summary>
    public void Clear()
    {
        this.world.Reset();
        this.teams.Clear();
        this.Shield.Clear();
        this.Mobs.Clear();
        this.mobEvents.Clear();
        this.mobAttacks.Clear();
    }

    /// <summary>
    /// 客户端断线时清理
    /// </summary>
    public void RemoveGuest(string guid)
    {
        this.teams.Forget(guid);
        this.world.RemoveGuest(guid);
    }

    /// <summary>
    /// 处理随上传帧到达的英雄动作命令。
    /// </summary>
    public void HandleCommands(
        string senderGuid,
        IReadOnlyList<HeroCommand> commands)
    {
        if (commands == null)
        {
            return;
        }

        foreach (var command in commands)
        {
            this.dispatcher.Handle(senderGuid, command);
        }
    }

    /// <summary>
    /// 用客户端最新 spawn 同步所有 ServerHeroCombatProxy 的项链
    /// </summary>
    public void SyncTalismans() => this.spawns.SyncAll(this.world.Actors);

    /// <summary>
    /// 读取待广播的动作结果
    /// </summary>
    public List<HeroOutcome> PeekOutcomes() => this.world.PeekEvents();

    public void CommitOutcomes(int count) => this.world.CommitEvents(count);

    /// <inheritdoc />
    public IReadOnlyList<EntityEvent> PeekEntityEvents() => this.mobEvents.Peek();

    /// <inheritdoc />
    public void CommitEntityEvents(int count) => this.mobEvents.Commit(count);
}
