namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;

/// <summary>Applies sampled motion, animation, affects and vitals; does not create, kill or repair mobs.</summary>
internal sealed class ClientMobStateApplier(
    GuestGameSession session, ClientMobModifierRegistry mobModifiers, WorldFrameTimeline frames)
{
    private readonly GuestGameSession session = session;
    private readonly ClientMobModifierRegistry mobModifiers = mobModifiers;
    private readonly WorldFrameTimeline frames = frames;
    private readonly ClientMobAffectState affectState = new();

    public void ApplyInterpolated(uint netId, Mob mob, long nowMs)
    {
        var nowSec = this.session.SyncedTimeMs / 1000.0;

        if (mob.destroyed)
        {
            return;
        }
        if (!this.frames.TryGetEntityBracket(
                netId, nowSec,
                out var prev, out var next,
                out var alpha, out var renderTimeSec))
        {
            this.affectState.ExpireIfDue(netId, mob, nowSec);
            return;
        }

        var sample = PoseInterpolator.Interpolate(
            prev.Pos, next.Pos, alpha);

        mob.setPosCase(
            sample.Cx, sample.Cy, sample.Xr, sample.Yr);
        mob.dir = sample.Dir;
        mob.dx = 0;
        mob.dy = 0;
        mob.bdx = 0;
        mob.bdy = 0;

        var handled = this.mobModifiers.DispatchClientUpdate(mob, nowMs);
        if (mob.destroyed)
        {
            return;
        }
        if (!handled)
        {
            RemoteMobAnimator.Apply(mob, prev.Anim, next.Anim, renderTimeSec);
        }

        this.affectState.ApplySnapshot(
            netId, mob, next.Affects ?? prev.Affects, nowSec);

        var serverLife = (int)Math.Round(
            prev.Life + ((next.Life - prev.Life) * alpha));

        mob.life = serverLife;
        mob.maxLife = next.MaxLife;
    }

    public void ApplyImmediate(EntityDynamic dynamic, Mob mob)
    {
        if (mob.destroyed)
        {
            return;
        }
        PoseInterpolator.SetPosition(
            mob,
            dynamic.Pos.CX + dynamic.Pos.XR,
            dynamic.Pos.CY + dynamic.Pos.XY);
        mob.dir = dynamic.Pos.DIR;
        mob.dx = dynamic.Pos.DX;
        mob.dy = dynamic.Pos.DY;
        mob.bdx = dynamic.Pos.BDX;
        mob.bdy = dynamic.Pos.BDY;

        mob.life = dynamic.Life;
        mob.maxLife = dynamic.MaxLife;

        var nowSec = this.session.SyncedTimeMs / 1000.0;
        var handled = this.mobModifiers.DispatchClientUpdate(mob, this.session.SyncedTimeMs);
        if (mob.destroyed)
        {
            return;
        }
        if (!handled)
        {
            RemoteMobAnimator.Apply(mob, dynamic.Anim, dynamic.Anim, nowSec);
        }

        this.affectState.ApplySnapshot(
            dynamic.NetId, mob, dynamic.Affects, nowSec);
    }

    public void Remove(uint netId) => this.affectState.Remove(netId);
    public void Reset() => this.affectState.Reset();
}
