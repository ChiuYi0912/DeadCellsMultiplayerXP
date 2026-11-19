namespace DeadCellsMultiplayerX.Server.World;

using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Server.Replication.Areas;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Replication.Entities.Mobs;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.World.Actors;
using Serilog;

/// <summary>Owns world state and components. WorldCoordinator owns its lifetime.</summary>
internal sealed class ServerWorld : IDisposable
{
    private readonly Thread gameThread = Thread.CurrentThread;
    private readonly Func<long> getTime;
    private readonly ILogger logger = Log.ForContext<ServerWorld>();

    public bool IsDisposed { get; private set; }
    public ClientFrameCollector ClientFrames { get; } = new();
    public ServerEntityRegistry Entities { get; } = new();
    private ServerEntityHandlerRegistry EntityHandlers { get; } = new();
    public ServerLevelAreaAffectRegistry AreaAffects { get; } = new();
    public EntityFrameBuilder EntityBuilder { get; }
    public WorldFrameBuilder Frames { get; }
    public ServerActorManager Ghosts { get; }

    public ServerWorld(Func<long> getTime)
    {
        this.getTime = getTime;
        this.EntityHandlers.Register(new ServerMobEntityHandler(this.Entities));
        this.EntityBuilder = new(this.Entities, this.EntityHandlers);
        this.Ghosts = new(this.ClientFrames, this.Entities, this.EntityBuilder.History,
            getTime, () => this.ClientFrames.Epoch);
        this.ClientFrames.CommandsReceived += this.Ghosts.HandleCommands;
        this.Frames = new(this.ClientFrames, this.EntityBuilder, this.Ghosts, this.AreaAffects);
    }

    public void AddGuest(string guid, Func<ClientFrameRangeRequest, Task<ClientFrameBundle>> requestFrames)
    {
        this.VerifyActive();
        this.ClientFrames.Register(guid, requestFrames);
    }

    public void RemoveGuest(string guid)
    {
        this.VerifyActive();
        this.ClientFrames.Unregister(guid);
        this.Ghosts.RemoveGuest(guid);
    }

    public WorldFrame? Tick(double dt, IReadOnlyCollection<string> guests)
    {
        this.VerifyActive();
        var now = this.getTime();
        this.ClientFrames.Tick(now);
        this.Ghosts.Tick(now, guests);
        return this.Frames.Build(dt, now);
    }

    public void BeginLevelEpoch()
    {
        this.VerifyActive();
        // Keep the existing reset semantics until per-level replacement is implemented.
        this.AreaAffects.Clear();
        this.Frames.BeginEpoch(this.getTime());
        this.Ghosts.BeginEpoch(this.Frames.Epoch);
    }

    private void VerifyActive()
    {
        this.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
    }

    private void VerifyGameThread()
    {
        if (Thread.CurrentThread != this.gameThread)
            throw new InvalidOperationException("Server world must be accessed on the game thread.");
    }

    public void Dispose()
    {
        this.VerifyGameThread();
        if (this.IsDisposed) return;
        this.IsDisposed = true;
        this.ClientFrames.CommandsReceived -= this.Ghosts.HandleCommands;
        // Hooks still need entity lookup while native proxies and modifiers detach.
        this.Cleanup(this.Ghosts.Clear, "ghosts and mob modifiers");
        this.Cleanup(this.Frames.Clear, "world frame history");
        this.Cleanup(this.ClientFrames.Clear, "client frames");
        this.Cleanup(this.Entities.ResetWorld, "entity registry");
        this.Cleanup(this.AreaAffects.Clear, "area affects");
    }

    private void Cleanup(Action cleanup, string resource)
    {
        try { cleanup(); }
        catch (Exception ex) { this.logger.Warning(ex, "World cleanup failed: {Resource}", resource); }
    }
}
