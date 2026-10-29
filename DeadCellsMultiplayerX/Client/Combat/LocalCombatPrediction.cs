using System;
using System.Collections.Generic;
using System.Linq;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using Serilog;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Combat
{
    /// <summary>
    /// 本地武器命中预测
    /// 致死命中走 orig,非致死命中生命预测
    /// 服务端确认存活时由 MobSyncManager 重建 mob
    /// </summary>
    internal sealed class LocalCombatPrediction
    {
        private const long TimeoutMs = 500;
        private const long ConfirmedLifeFallbackMs = 2000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "LocalCombatPrediction");

        private readonly GuestClientSession session;
        private readonly Dictionary<uint, List<PredictedMob>> pendingByMob = [];
        private readonly Stack<PredictionBatch> activeBatches = [];
        private readonly Dictionary<long, PredictionBatch> batchesByToken = [];
        private readonly Dictionary<uint, ConfirmedLife> confirmedLife = [];
        private long nextToken;

        public LocalCombatPrediction(GuestClientSession session)
        {
            this.session = session;
        }

        private sealed class PredictionBatch
        {
            public long Token;
            public long ActionId;
            public long ExpiresAtMs;
            public readonly Dictionary<uint, PredictedMob> Mobs = [];
        }

        private sealed class PredictedMob
        {
            public PredictedMob(Mob mob, PredictionBatch batch)
            {
                Mob = mob;
                Batch = batch;
            }

            public Mob Mob { get; }
            public PredictionBatch Batch { get; }
            public int Life;
            public bool Dead;
            public bool Confirmed;
            public bool ServerDead;
            public long ConfirmServerTimeMs;
        }

        private sealed class ConfirmedLife
        {
            public int Life;
            public long ServerTimeMs;
        }

        public long BeginWeapon()
        {
            long token = ++nextToken;

            var batch = new PredictionBatch
            {
                Token = token,
                ExpiresAtMs = session.SyncedTimeMs + TimeoutMs,
            };

            activeBatches.Push(batch);
            batchesByToken[token] = batch;
            return token;
        }

        public void EndWeapon()
        {
            if (activeBatches.Count > 0)
                activeBatches.Pop();
        }

        public void CommitWeapon(long token, long actionId)
        {
            if (!batchesByToken.TryGetValue(token, out var batch))
                return;

            batch.ActionId = actionId;

            if (batch.Mobs.Count == 0)
                batchesByToken.Remove(token);
        }

        /// <summary>
        /// 处理本地 hero 的武器命中；返回 true 表示已由预测处理
        /// </summary>
        public bool TryHandleLocalHit(
            AttackData atk,
            Mob mob,
            Action applyOriginal,
            Action playVisual)
        {
            if (mob == null || mob.destroyed)
                return false;

            if (activeBatches.Count == 0)
                return false;

            var world = session.RemoteWorld;
            if (world == null
                || !world.Mobs.Receiver.TryGetNetId(mob, out uint netId))
            {
                return false;
            }

            if (IsPredictedDead(netId))
                return true;

            int damage = Math.Max(0, atk.finalDmg);
            if (damage == 0)
                return false;

            bool lethal = mob.life - damage <= 0;

            if (lethal)
            {
                // 真实命中：触发原版 applyHit、onDamage、onDie、掉落/统计
                using (session.DeathGate.Allow(mob))
                {
                    applyOriginal();
                }
            }
            else
            {
                mob.life = Math.Max(0, mob.life - damage);
                playVisual();
            }

            var batch = activeBatches.Peek();

            if (!batch.Mobs.TryGetValue(netId, out var predicted))
            {
                predicted = new PredictedMob(mob, batch);
                batch.Mobs[netId] = predicted;

                if (!pendingByMob.TryGetValue(netId, out var list))
                {
                    list = [];
                    pendingByMob[netId] = list;
                }

                list.Add(predicted);
            }

            predicted.Life = mob.destroyed ? 0 : Math.Max(0, mob.life);
            predicted.Dead = mob.destroyed || predicted.Life <= 0;

            return true;
        }

        /// <summary>
        /// Ghost 帧结算写入的权威生命
        /// </summary>
        public void ApplyConfirmedLife(
            uint netId,
            int life,
            long serverTimeMs)
        {
            confirmedLife[netId] = new ConfirmedLife
            {
                Life = life,
                ServerTimeMs = serverTimeMs,
            };
        }

        /// <summary>
        /// 计算本帧显示生命；不处理可见性
        /// </summary>
        public int ResolveLife(
            uint netId,
            int serverLife,
            long renderTimeMs)
        {
            int life = serverLife;

            if (confirmedLife.TryGetValue(netId, out var confirmed))
            {
                if (renderTimeMs < confirmed.ServerTimeMs
                    + SnapshotTiming.InterpolationDelayMs)
                {
                    // Ghost 帧结果优先于 100ms 插值后的旧生命
                    life = confirmed.Life;
                }
                else
                {
                    confirmedLife.Remove(netId);
                }
            }

            if (!pendingByMob.TryGetValue(netId, out var list)
                || list.Count == 0)
            {
                return life;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var predicted = list[i];

                if (predicted.Confirmed
                    && !predicted.ServerDead
                    && renderTimeMs >= predicted.ConfirmServerTimeMs
                    + SnapshotTiming.InterpolationDelayMs)
                {
                    list.RemoveAt(i);
                    predicted.Batch.Mobs.Remove(netId);

                    if (predicted.Batch.Mobs.Count == 0)
                        batchesByToken.Remove(predicted.Batch.Token);

                    continue;
                }

                life = Math.Min(life, predicted.Life);
            }

            if (list.Count == 0)
                pendingByMob.Remove(netId);

            return life;
        }

        public void Tick(long nowMs)
        {
            List<long> remove = [];

            foreach (var batch in batchesByToken.Values)
            {
                bool unconfirmedTimeout =
                    batch.Mobs.Count > 0
                    && batch.Mobs.Values.Any(p => !p.Confirmed)
                    && nowMs >= batch.ExpiresAtMs;

                bool uncommittedTimeout =
                    batch.ActionId == 0 && nowMs >= batch.ExpiresAtMs;

                bool confirmedAliveTimeout =
                    batch.Mobs.Count > 0
                    && batch.Mobs.Values.All(p => p.Confirmed && !p.ServerDead)
                    && nowMs >= batch.ExpiresAtMs + TimeoutMs;

                if (unconfirmedTimeout
                    || uncommittedTimeout
                    || confirmedAliveTimeout)
                    remove.Add(batch.Token);
            }

            foreach (var token in remove)
            {
                if (batchesByToken.TryGetValue(token, out var batch))
                    RemoveBatch(batch);
            }

            List<uint> expiredConfirmed = [];
            foreach (var (netId, confirmed) in confirmedLife)
            {
                if (nowMs >= confirmed.ServerTimeMs + ConfirmedLifeFallbackMs)
                    expiredConfirmed.Add(netId);
            }

            foreach (var netId in expiredConfirmed)
                confirmedLife.Remove(netId);
        }

        /// <summary>
        /// 确认本地 caster 的 HeroActionEvent
        /// 必须在 MobSyncManager 的 receiver.Remove 之前调用
        /// </summary>
        public void ConfirmFrame(WorldFrame frame)
        {
            if (frame?.Actions == null || frame.Actions.Count == 0)
                return;

            var world = session.RemoteWorld;
            if (world == null)
                return;

            string localGuid = world.LocalGuid;

            foreach (var evt in frame.Actions)
            {
                if (evt == null) continue;
                if (evt.CasterGuid != localGuid) continue;
                if (evt.Epoch != frame.Epoch) continue;

                List<PredictionBatch> matches = [];
                foreach (var batch in batchesByToken.Values)
                {
                    if (batch.ActionId == evt.ActionId)
                        matches.Add(batch);
                }

                foreach (var batch in matches)
                    ConfirmBatch(batch, frame, evt);
            }
        }

        public void ConfirmDespawn(uint netId)
        {
            if (!pendingByMob.TryGetValue(netId, out var list))
                return;

            foreach (var predicted in list)
            {
                predicted.Confirmed = true;
                predicted.ServerDead = true;
                predicted.Dead = true;
                predicted.Life = 0;
            }
        }

        public void OnRemoved(uint netId)
        {
            if (!pendingByMob.Remove(netId, out var list))
            {
                confirmedLife.Remove(netId);
                return;
            }

            foreach (var predicted in list)
            {
                predicted.Confirmed = true;
                predicted.ServerDead = true;
            }

            List<PredictionBatch> empty = [];
            foreach (var batch in batchesByToken.Values)
            {
                if (batch.Mobs.Remove(netId) && batch.Mobs.Count == 0)
                    empty.Add(batch);
            }

            foreach (var batch in empty)
                batchesByToken.Remove(batch.Token);

            confirmedLife.Remove(netId);
        }

        /// <summary>
        /// 重建新实体后清除该 NetId 的旧预测/覆盖
        /// </summary>
        public void ClearNetId(uint netId)
        {
            pendingByMob.Remove(netId);
            confirmedLife.Remove(netId);

            List<PredictionBatch> empty = [];
            foreach (var batch in batchesByToken.Values)
            {
                if (batch.Mobs.Remove(netId) && batch.Mobs.Count == 0)
                    empty.Add(batch);
            }

            foreach (var batch in empty)
                batchesByToken.Remove(batch.Token);
        }

        public bool IsPredictedDead(uint netId)
        {
            return pendingByMob.TryGetValue(netId, out var list)
                && list.Any(p => p.ServerDead || p.Dead);
        }

        /// <summary>
        /// 是否还有未结算的本地死亡预测；未确认前不要重建实体
        /// </summary>
        public bool HasPendingDeathPrediction(uint netId)
        {
            return pendingByMob.TryGetValue(netId, out var list)
                && list.Any(p => p.Dead || p.ServerDead);
        }

        public void Clear()
        {
            pendingByMob.Clear();
            activeBatches.Clear();
            batchesByToken.Clear();
            confirmedLife.Clear();
        }

        private void ConfirmBatch(
            PredictionBatch batch,
            WorldFrame frame,
            HeroActionEvent evt)
        {
            foreach (var entry in batch.Mobs.ToList())
            {
                uint netId = entry.Key;
                var predicted = entry.Value;

                MobDynamic? current = frame.Mobs?
                    .FirstOrDefault(m => m != null && m.NetId == netId);

                bool serverDead = frame.DespawnedMobs?.Contains(netId) == true
                    || current == null
                    || current.Life <= 0;

                predicted.Confirmed = true;
                predicted.ConfirmServerTimeMs = evt.ServerTimeMs;

                if (serverDead)
                {
                    predicted.ServerDead = true;
                    predicted.Dead = true;
                    predicted.Life = 0;

                    // 服务端已确认死亡但本地尚未走原生死亡时，立即补死亡特效
                    if (!predicted.Mob.destroyed)
                        session.ForceNativeDeath(predicted.Mob);

                    continue;
                }

                // 服务端认为还活着但本地预测了死亡：交给重建流程
                if (predicted.Dead)
                {
                    RemovePrediction(netId, predicted);
                    Logger.Debug(
                        "Prediction death rolled back: netId={NetId} action={ActionId}",
                        netId, evt.ActionId);
                }
            }
        }

        private void RemovePrediction(uint netId, PredictedMob predicted)
        {
            if (pendingByMob.TryGetValue(netId, out var list))
            {
                list.Remove(predicted);
                if (list.Count == 0)
                    pendingByMob.Remove(netId);
            }

            predicted.Batch.Mobs.Remove(netId);

            if (predicted.Batch.Mobs.Count == 0)
                batchesByToken.Remove(predicted.Batch.Token);
        }

        private void RemoveBatch(PredictionBatch batch)
        {
            foreach (var (netId, predicted) in batch.Mobs)
            {
                if (!pendingByMob.TryGetValue(netId, out var list))
                    continue;

                list.Remove(predicted);

                if (list.Count == 0)
                    pendingByMob.Remove(netId);
            }

            batchesByToken.Remove(batch.Token);
        }
    }
}
