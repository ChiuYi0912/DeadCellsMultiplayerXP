using System;
using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Client.Snapshot;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Core.Tools;
using Serilog;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Mobs
{
    /// <summary>
    /// 客户端怪物同步装置
    /// 消费世界帧中的怪物数据并驱动真实 Mob 表现
    /// </summary>
    internal sealed class MobSyncManager
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobSyncManager");

        private readonly GuestClientSession session;
        private readonly RemoteWorld remoteWorld;
        private readonly WorldFrameManager frames;
        private readonly ClientMobReceiver receiver;
        private readonly MobAffectStateManager affectState;

        /// <summary>
        /// 记录mobid
        /// </summary>
        private readonly HashSet<uint> frameNetIds = [];

        private int lastEpoch = -1;
        private bool loggedFirstMob;

        public MobSyncManager(
            GuestClientSession session,
            RemoteWorld remoteWorld,
            WorldFrameManager frames)
        {
            this.session = session;
            this.remoteWorld = remoteWorld;
            this.frames = frames;
            receiver = new ClientMobReceiver(session, remoteWorld);
            affectState = new MobAffectStateManager();
        }

        public ClientMobReceiver Receiver => receiver;

        public bool IsRemoteMob(Mob mob) => receiver.IsRemoteMob(mob);

        public bool IsDeathSuppressed(Mob mob) => receiver.IsDeathSuppressed(mob);

        /// <summary>
        /// 应用一个连续世界帧中的怪物数据
        /// </summary>
        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            // epoch 变化时清空旧怪物与缓存
            if (frame.Epoch != lastEpoch)
            {
                lastEpoch = frame.Epoch;
                session.Prediction.Clear();
                receiver.Clear();
                affectState.Reset();
            }

            // 必须在 receiver.Remove 之前确认，避免预测死亡的 mob 已被移除
            session.Prediction.ConfirmFrame(frame);

            receiver.RetryPending();

            if (frame.SpawnedMobs != null)
            {
                foreach (var spawn in frame.SpawnedMobs)
                    receiver.OnSpawned(spawn);
            }

            // 服务端帧已给出 Life<=0 但还没进 DespawnedMobs,补充死亡
            ForceFrameDeadMobs(frame);

            // 本地预测致死后服务端仍存活,使用 NetId 重建
            RepairDestroyedMobs(frame);

            if (frame.DespawnedMobs != null)
            {
                foreach (var netId in frame.DespawnedMobs)
                {
                    session.Prediction.ConfirmDespawn(netId);
                    receiver.Remove(netId);
                    affectState.Remove(netId);
                }
            }

            // 以服务端当前帧的集合为准清理本地多余怪物
            frameNetIds.Clear();
            if (frame.Mobs != null)
            {
                foreach (var mob in frame.Mobs)
                {
                    if (mob == null) continue;
                    frameNetIds.Add(mob.NetId);

                    // 有动态状态但没有本地实体也没有待生成数据时请求重同步
                    if (!receiver.HasOrPending(mob.NetId))
                        frames.RequestResync("mob spawn missing");
                }
            }

            var localMobs = new List<Mob>(receiver.All);
            foreach (var mob in localMobs)
            {
                if (mob == null || mob.destroyed) continue;
                if (!receiver.TryGetNetId(mob, out var netId)) continue;
                if (!frameNetIds.Contains(netId))
                {
                    session.Prediction.ConfirmDespawn(netId);
                    receiver.Remove(netId);
                    affectState.Remove(netId);
                }
            }

            if (!loggedFirstMob && frame.Mobs != null && frame.Mobs.Count > 0)
            {
                loggedFirstMob = true;
                Logger.Information(
                    "First mob frame: epoch={Epoch} seq={Sequence} mobs={Mobs}",
                    frame.Epoch, frame.Sequence, frame.Mobs.Count);
            }
        }

        /// <summary>
        /// 同步mobs循环
        /// </summary>
        /// <param name="nowMs"></param>
        public void Tick(long nowMs)
        {
            session.Prediction.Tick(nowMs);
            receiver.RetryPending();

            double nowSec = session.SyncedTimeMs / 1000.0;

            foreach (var mob in receiver.All)
            {
                if (mob == null || mob.destroyed) continue;
                if (!receiver.TryGetNetId(mob, out var netId)) continue;

                if (!frames.TryGetMobBracket(
                        netId, nowSec,
                        out var prev, out var next,
                        out double alpha, out double renderTimeSec))
                {
                    affectState.ExpireIfDue(netId, mob, nowSec);
                    continue;
                }

                double prevX = prev.Pos.CX + prev.Pos.XR;
                double prevY = prev.Pos.CY + prev.Pos.XY;
                double nextX = next.Pos.CX + next.Pos.XR;
                double nextY = next.Pos.CY + next.Pos.XY;

                double x = prevX + (nextX - prevX) * alpha;
                double y = prevY + (nextY - prevY) * alpha;

                int cx = (int)Math.Floor(x);
                int cy = (int)Math.Floor(y);

                mob.setPosCase(cx, cy, x - cx, y - cy);
                mob.dir = alpha < 0.5 ? prev.Pos.DIR : next.Pos.DIR;
                mob.dx = 0;
                mob.dy = 0;
                mob.bdx = 0;
                mob.bdy = 0;

                if (!remoteWorld.MobModifiers.DispatchClientUpdate(mob, nowMs))
                    RemoteMobAnimator.Apply(mob, prev.Anim, next.Anim, renderTimeSec);

                // 同步affect
                affectState.ApplySnapshot(netId, mob, next.Affects ?? prev.Affects, nowSec);

                int serverLife = (int)Math.Round(
                    prev.Life + (next.Life - prev.Life) * alpha);
                long renderTimeMs = (long)Math.Round(renderTimeSec * 1000);

                int life = session.Prediction.ResolveLife(
                    netId, serverLife, renderTimeMs);

                mob.life = life;
                mob.maxLife = next.MaxLife;
            }
        }

        /// <summary>
        /// 执行由服务端发送的mob死亡指令
        /// </summary>
        private void ForceFrameDeadMobs(WorldFrame frame)
        {
            if (frame.Mobs == null) return;

            foreach (var dynamic in frame.Mobs)
            {
                if (dynamic == null || dynamic.Life > 0) continue;
                if (receiver.Get(dynamic.NetId) is not { destroyed: false } mob)
                    continue;

                session.Prediction.ConfirmDespawn(dynamic.NetId);
                session.ForceNativeDeath(mob);
            }
        }

        /// <summary>
        /// 预测错误时,需要客户端重建对应 mob
        /// </summary>
        private void RepairDestroyedMobs(WorldFrame frame)
        {
            if (frame.Mobs == null) return;

            foreach (var dynamic in frame.Mobs)
            {
                if (dynamic == null || dynamic.Life <= 0) continue;

                // 本地预测致死还未确认前，服务端旧帧仍可能显示存活
                if (session.Prediction.HasPendingDeathPrediction(dynamic.NetId))
                    continue;

                if (!receiver.IsMissingOrDestroyed(dynamic.NetId)) continue;

                if (receiver.TryRecreate(dynamic.NetId, out var mob)
                    && mob != null)
                {
                    ApplyDynamicNow(dynamic, mob);
                    continue;
                }

                // 重建失败但旧实体已销毁,清掉旧映射，触发后续 resync 恢复
                if (receiver.Get(dynamic.NetId) is { destroyed: true })
                    receiver.Remove(dynamic.NetId);
            }
        }

        private void ApplyDynamicNow(MobDynamic dynamic, Mob mob)
        {
            double x = dynamic.Pos.CX + dynamic.Pos.XR;
            double y = dynamic.Pos.CY + dynamic.Pos.XY;

            int cx = (int)Math.Floor(x);
            int cy = (int)Math.Floor(y);

            mob.setPosCase(cx, cy, x - cx, y - cy);
            mob.dir = dynamic.Pos.DIR;
            mob.dx = dynamic.Pos.DX;
            mob.dy = dynamic.Pos.DY;
            mob.bdx = dynamic.Pos.BDX;
            mob.bdy = dynamic.Pos.BDY;

            mob.life = dynamic.Life;
            mob.maxLife = dynamic.MaxLife;

            double nowSec = session.SyncedTimeMs / 1000.0;
            if (!remoteWorld.MobModifiers.DispatchClientUpdate(
                    mob, session.SyncedTimeMs))
            {
                RemoteMobAnimator.Apply(mob, dynamic.Anim, dynamic.Anim, nowSec);
            }
            affectState.ApplySnapshot(
                dynamic.NetId, mob, dynamic.Affects, nowSec);

            session.Prediction.ClearNetId(dynamic.NetId);
        }

        public void Clear()
        {
            session.Prediction.Clear();
            receiver.Clear();
            affectState.Reset();
        }
    }
}
