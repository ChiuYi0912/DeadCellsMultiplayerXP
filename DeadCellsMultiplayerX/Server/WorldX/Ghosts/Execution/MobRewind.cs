using System;
using System.Collections.Generic;
using dc.en;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Server.WorldX.Hubs;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Execution
{
    /// <summary>
    /// 武器执行前的服务端 mob 位置回退
    /// 让服务端命中判定使用客户端开火时刻看到的位置
    /// </summary>
    internal sealed class MobRewind
    {
        private const long MaxRewindMs = 300;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobRewind");

        private readonly ServerMobRegistry registry;
        private readonly ServerMobHistory history;

        public MobRewind(
            ServerMobRegistry registry,
            ServerMobHistory history)
        {
            this.registry = registry;
            this.history = history;
        }

        /// <summary>
        /// 回退 mob 位置；返回的 scope 在武器执行后恢复原始位置
        /// </summary>
        public IDisposable? Begin(
            Level level,
            long clientTimeMs,
            long serverTimeMs)
        {
            if (level == null)
                return null;

            long targetMs = clientTimeMs > 0
                ? clientTimeMs - SnapshotTiming.InterpolationDelayMs
                : serverTimeMs - SnapshotTiming.InterpolationDelayMs;

            targetMs = Math.Clamp(
                targetMs,
                serverTimeMs - MaxRewindMs,
                serverTimeMs);

            var mobs = CollectMobs(level);
            var saved = new List<RawPose>(mobs.Count);

            foreach (var mob in mobs)
            {
                if (!registry.TryGetNetId(mob, out uint netId))
                    continue;

                if (!history.TryGetPose(netId, targetMs, out var pose))
                    continue;

                saved.Add(new RawPose(
                    mob, mob.cx, mob.cy, mob.xr, mob.yr, mob.dir));

                Apply(mob, pose);
            }

            if (saved.Count == 0)
                return null;

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
                for (int i = 0; i < entities.length; i++)
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
            if (mobs == null) return;

            for (int i = 0; i < mobs.length; i++)
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
            if (mob == null || mob.destroyed) return;
            if (!seen.Add(mob)) return;

            result.Add(mob);
        }

        private static void Apply(Mob mob, MobPose pose)
        {
            int cx = (int)Math.Floor(pose.X);
            int cy = (int)Math.Floor(pose.Y);

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

        private sealed class Scope : IDisposable
        {
            private readonly List<RawPose> saved;
            private bool disposed;

            public Scope(List<RawPose> saved)
            {
                this.saved = saved;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                foreach (var pose in saved)
                {
                    var mob = pose.Mob;
                    if (mob == null || mob.destroyed) continue;

                    mob.cx = pose.Cx;
                    mob.cy = pose.Cy;
                    mob.xr = pose.Xr;
                    mob.yr = pose.Yr;
                    mob.dir = pose.Dir;
                }
            }
        }
    }
}
