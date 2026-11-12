namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Networking.Time;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using Serilog;

/// <summary>
/// 武器执行前的服务端 mob 位置回退
/// 让服务端命中判定使用客户端开火时刻看到的位置
/// </summary>
internal sealed class MobRewind(
    ServerEntityRegistry registry,
    ServerEntityHistory history)
{
    private const long MAX_REWIND_MS = 300;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobRewind");

    private readonly ServerEntityRegistry registry = registry;
    private readonly ServerEntityHistory history = history;

    /// <summary>
    /// 回退 mob 位置；返回的 scope 在武器执行后恢复原始位置
    /// </summary>
    public IDisposable? Begin(
        Level level,
        long clientTimeMs,
        long serverTimeMs)
    {
        if (level == null)
        {
            return null;
        }

        var targetMs = clientTimeMs > 0
            ? clientTimeMs - SnapshotTiming.INTERPOLATION_DELAY_MS
            : serverTimeMs - SnapshotTiming.INTERPOLATION_DELAY_MS;

        targetMs = Math.Clamp(
            targetMs,
            serverTimeMs - MAX_REWIND_MS,
            serverTimeMs);

        var mobs = CollectMobs(level);
        var saved = new List<RawPose>(mobs.Count);

        foreach (var mob in mobs)
        {
            if (!this.registry.TryGetNetId(mob, out var netId))
            {
                continue;
            }

            if (!this.history.TryGetPose(netId, targetMs, out var pose))
            {
                continue;
            }

            saved.Add(new RawPose(
                mob, mob.cx, mob.cy, mob.xr, mob.yr, mob.dir));

            Apply(mob, pose);
        }

        if (saved.Count == 0)
        {
            return null;
        }

        Logger.Debug(
            "Mob rewind: target={Target} server={Server} mobs={Mobs}",
            targetMs, serverTimeMs, saved.Count);

        return new Scope(saved);
    }

    private static List<Mob> CollectMobs(Level level)
    {
        var result = new List<Mob>();
        var seen = new HashSet<Mob>();

        CollectTeam(level.teamMob, result, seen);
        CollectTeam(level.teamProp, result, seen);

        var entities = level.entities;
        if (entities != null)
        {
            for (var i = 0; i < entities.length; i++)
            {
                Add(
                    AffectCodec.ToObject<Mob>(entities.getDyn(i)),
                    result, seen);
            }
        }

        return result;
    }

    private static void CollectTeam(
        dc.tool.Team? team,
        List<Mob> result,
        HashSet<Mob> seen)
    {
        var mobs = team?.asMobs;
        if (mobs == null)
        {
            return;
        }

        for (var i = 0; i < mobs.length; i++)
        {
            Add(
                AffectCodec.ToObject<Mob>(mobs.getDyn(i)),
                result, seen);
        }
    }

    private static void Add(
        Mob? mob,
        List<Mob> result,
        HashSet<Mob> seen)
    {
        if (mob == null || mob.destroyed)
        {
            return;
        }

        if (!seen.Add(mob))
        {
            return;
        }

        result.Add(mob);
    }

    private static void Apply(Mob mob, EntityPose pose)
    {
        var cx = (int)Math.Floor(pose.X);
        var cy = (int)Math.Floor(pose.Y);

        mob.cx = cx;
        mob.cy = cy;
        mob.xr = pose.X - cx;
        mob.yr = pose.Y - cy;
        mob.dir = pose.Dir;
    }

    private readonly record struct RawPose(
        Mob Mob,
        int Cx,
        int Cy,
        double Xr,
        double Yr,
        int Dir);

    private sealed class Scope(List<RawPose> saved) : IDisposable
    {
        private readonly List<RawPose> saved = saved;
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            foreach (var pose in this.saved)
            {
                var mob = pose.Mob;
                if (mob == null || mob.destroyed)
                {
                    continue;
                }

                mob.cx = pose.Cx;
                mob.cy = pose.Cy;
                mob.xr = pose.Xr;
                mob.yr = pose.Yr;
                mob.dir = pose.Dir;
            }
        }
    }
}
