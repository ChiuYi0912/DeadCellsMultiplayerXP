namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.Session;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using Serilog;

/// <summary>
/// 服务端 Ghost 入口。
/// 思路：聚合世界状态、队伍、仇恨、mob 事件与动作管线；
/// 对外暴露 ActionJournal 等诊断入口，动作执行细节下沉到 ActionPipeline/Modules。
/// </summary>
internal sealed class ServerActorManager : IServerEntityEventSource
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

    private readonly ServerSession session;
    private readonly WorldState world;
    private readonly SpawnSynchronizer spawns;
    private readonly ActionPipeline pipeline;
    private readonly ServerTeamManager teams;
    private readonly ServerMobEventOutbox mobEvents;
    private readonly ServerMobAttackRouter mobAttacks;

    public ServerActorManager(ServerSession session)
    {
        this.session = session;

        this.world = new WorldState();
        this.spawns = new SpawnSynchronizer(session.ClientFrames);
        this.teams = new ServerTeamManager(
            this.world,
            session.ClientFrames,
            this.spawns,
            () => Game.Class.ME);
        this.Aggro = new ServerAggroManager(
            this.teams,
            session.Entities,
            () => Game.Class.ME);
        this.pipeline = ActionPipeline.CreateDefault(
            this.world,
            this.spawns,
            session.ClientFrames,
            session.Entities,
            session.EntityPublisher.History,
            () => session.CurrentTimeStamp,
            () => Game.Class.ME);

        this.mobEvents = new ServerMobEventOutbox();
        this.mobAttacks = new ServerMobAttackRouter(events: this.mobEvents);
        var mobModifiers = new ServerMobModifierRegistry(
            session, this.teams, this.mobAttacks, this.mobEvents);
        this.Mobs = new ServerMobRuntime(
            mobModifiers, this.Aggro, () => session.CurrentTimeStamp);
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
    /// 每个游戏 tick 同步 Ghost 位置/队伍并推进仇恨协调
    /// </summary>
    public void Tick(double dt, long nowMs, IReadOnlyCollection<string> guests)
    {
        this.teams.Sync(guests, nowMs);
        this.Aggro.Tick(nowMs);
        this.Mobs.Tick(nowMs);
        this.mobAttacks.Tick(nowMs);
    }

    /// <summary>
    /// 通过底层 Hero 查找可战斗 Ghost
    /// </summary>
    public bool TryGetCombatant(Entity entity, out ServerHeroCombatView combatant)
        => this.teams.TryGet(entity, out combatant);

    /// <summary>
    /// 世界重置,写入新 epoch 并清空 Ghost 状态
    /// </summary>
    public void BeginEpoch(int value)
    {
        this.world.BeginEpoch(value);
        this.teams.Clear();

        // A new level epoch invalidates every per-mob modifier binding.
        this.Mobs.Clear();
        this.mobEvents.Clear();
        this.mobAttacks.Clear();

        Logger.Information(
            "Ghost action epoch begin: {Epoch}", value);
    }

    /// <summary>
    /// 清理 ServerHeroCombatProxy 与待广播事件
    /// </summary>
    public void Clear()
    {
        this.world.Reset();
        this.teams.Clear();
        this.Mobs.Clear();
        this.mobEvents.Clear();
        this.mobAttacks.Clear();
    }

    /// <summary>
    /// 客户端断线时清理它的 Ghost 与准入状态
    /// </summary>
    public void RemoveGuest(string guid)
    {
        this.teams.Forget(guid);
        this.world.RemoveGuest(guid);
    }

    /// <summary>
    /// 处理客户端上报的英雄动作
    /// </summary>
    public void HandleAction(string senderGuid, HeroAction? action)
        => this.pipeline.Handle(senderGuid, action);

    /// <summary>
    /// 用客户端最新 spawn 同步所有 ServerHeroCombatProxy 的项链
    /// </summary>
    public void SyncTalismans() => this.spawns.SyncAll(this.world.Actors);

    /// <summary>
    /// 取出本帧待广播的动作事件
    /// </summary>
    public List<HeroActionEvent> DrainActions() => this.world.DrainEvents();

    /// <inheritdoc />
    public IReadOnlyList<EntityEvent> DrainEntityEvents()
        => this.mobEvents.Drain();
}
