namespace DeadCellsMultiplayerX.Server.World;

using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Server.World.Replication.Areas;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using DeadCellsMultiplayerX.Server.World.Replication.Entities.Mobs;
using DeadCellsMultiplayerX.Server.World.Replication.Frames;
using DeadCellsMultiplayerX.Server.World.Actors;
using Serilog;

/// <summary>Owns world state and components. WorldCoordinator owns its lifetime.</summary>
internal sealed class ServerWorld : IDisposable
{
    private readonly Thread gameThread = Thread.CurrentThread;
    private readonly Func<long> getTime;
    private readonly ILogger logger = Log.ForContext<ServerWorld>();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken token;

    public bool IsDisposed { get; private set; }
    public bool IsRunning { get; private set; }
    public int Epoch { get; }
    public ClientFrameCollector ClientFrames { get; } = new();
    public ServerEntityRegistry Entities { get; } = new();
    public ServerLevelAreaAffectRegistry AreaAffects { get; } = new();
    public EntityFrameBuilder EntityBuilder { get; }
    public WorldFrameBuilder Frames { get; }
    public ServerActorRuntime Actors { get; }

    public ServerWorld(Func<long> getTime, int epoch)
    {
        this.getTime = getTime;
        this.Epoch = epoch;
        this.token = this.lifetime.Token;
        var entityHandlers = new ServerEntityHandlerRegistry();
        entityHandlers.Register(new ServerMobEntityHandler(this.Entities));
        this.EntityBuilder = new(this.Entities, entityHandlers);
        this.Actors = new(this.ClientFrames, this.Entities, this.EntityBuilder.History,
            getTime, () => this.ClientFrames.Epoch);
        this.ClientFrames.CommandsReceived += this.Actors.HandleCommands;
        this.Frames = new(this.ClientFrames, this.EntityBuilder, this.Actors, this.AreaAffects);
        // Initialize before native creation registers entities and attaches modifiers.
        this.Frames.BeginEpoch(epoch);
        this.Actors.BeginEpoch(epoch);
    }

    public void AddGuest(string guid, Func<ClientFrameRangeRequest, Task<ClientFrameBundle>> requestFrames)
    {
        this.VerifyActive();
        this.ClientFrames.Register(guid, request => requestFrames(request).WaitAsync(this.token));
    }

    public void RemoveGuest(string guid)
    {
        this.VerifyActive();
        this.ClientFrames.Unregister(guid);
        this.Actors.RemoveGuest(guid);
    }

    public WorldFrame? Tick(double dt, IReadOnlyCollection<string> guests)
    {
        this.VerifyActive();
        if (!this.IsRunning)
        {
            return null;
        }
        var now = this.getTime();
        this.ClientFrames.Tick(now);
        this.Actors.Tick(now, guests);
        return this.Frames.Build(dt, now);
    }

    public void Start()
    {
        this.VerifyActive();
        this.IsRunning = true;
    }

    public void Stop()
    {
        this.VerifyGameThread();
        this.IsRunning = false;
    }

    public void AcceptFrame(string guid, ClientWorldFrame frame)
    {
        this.VerifyActive();
        if (this.IsRunning && frame != null && frame.Epoch == this.Epoch)
        {
            this.ClientFrames.OnFrame(guid, frame);
        }

    }

    private void VerifyActive()
    {
        this.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
    }

    private void VerifyGameThread()
    {
        if (Thread.CurrentThread != this.gameThread)
        {

            throw new InvalidOperationException("Server world must be accessed on the game thread.");
        }

    }

    public void Dispose()
    {
        this.VerifyGameThread();
        if (this.IsDisposed)
        {
            return;
        }
        this.IsDisposed = true;
        this.IsRunning = false;
        this.Cleanup(this.lifetime.Cancel, "world cancellation");
        this.ClientFrames.CommandsReceived -= this.Actors.HandleCommands;
        // Hooks still need entity lookup while native proxies and modifiers detach.
        this.Cleanup(this.Actors.Clear, "ghosts and mob modifiers");
        this.Cleanup(this.Frames.Clear, "world frame history");
        this.Cleanup(this.ClientFrames.Clear, "client frames");
        this.Cleanup(this.Entities.ResetWorld, "entity registry");
        this.Cleanup(this.AreaAffects.Clear, "area affects");
        this.lifetime.Dispose();
    }

    private void Cleanup(Action cleanup, string resource)
    {
        try
        { cleanup(); }
        catch (Exception ex) { this.logger.Warning(ex, "World cleanup failed: {Resource}", resource); }
    }
}
