using System;
using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Serializers;
using Serilog;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Mobs
{
    /// <summary>
    /// 客户端怪物 affect 快照镜像
    /// 服务端快照是唯一权威 本地只负责校准时长 清除旧状态并应用快照
    /// </summary>
    internal sealed class MobAffectStateManager
    {
        private const int MaxEntries = 256;
        private const int MaxOuterIndex = 256;
        private const int MaxInnerIndex = 256;
        private const double DriftLogThresholdSec = 0.25;

        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "MobAffectStateManager");

        /// <summary>
        /// 当前同步时间命中的快照条目
        /// </summary>
        private readonly List<AffectEntry> active = [];

        /// <summary>
        /// 本次快照覆盖的槽位
        /// </summary>
        private readonly HashSet<int> snapshotIndexes = [];

        /// <summary>
        /// 本次快照包含的 effect 类型
        /// </summary>
        private readonly HashSet<int> snapshotTypes = [];

        /// <summary>
        /// 本次快照包含的完整身份集合
        /// </summary>
        private readonly HashSet<AffectKey> snapshotKeys = [];

        /// <summary>
        /// 每个 mob 被服务端快照接管过的槽位
        /// </summary>
        private readonly Dictionary<uint, HashSet<int>> serverOwnedIndexes = [];

        /// <summary>
        /// 上一次快照包含的 affect 身份 用于检测服务端移除
        /// </summary>
        private readonly Dictionary<uint, HashSet<AffectKey>> lastKeys = [];

        /// <summary>
        /// 当前快照的绝对过期时间
        /// </summary>
        private readonly Dictionary<uint, double> nextExpiry = [];

        /// <summary>
        /// 用服务端快照替换指定怪物的 affect 状态
        /// 快照为空也会清除旧的权威槽位 避免状态残留
        /// </summary>
        public void ApplySnapshot(
            uint netId,
            Mob mob,
            List<AffectEntry>? entries,
            double nowSec)
        {
            if (mob == null || mob.destroyed) return;
            if (!double.IsFinite(nowSec)) return;

            var affects = mob.affects;
            if (affects == null) return;

            double baseFps = mob.cd?.baseFps ?? 60.0;
            double fps = baseFps > 0.0 && double.IsFinite(baseFps) ? baseFps : 60.0;

            active.Clear();
            snapshotIndexes.Clear();
            snapshotTypes.Clear();
            snapshotKeys.Clear();

            double maxEndSec = 0.0;

            if (entries != null)
            {
                int count = Math.Min(entries.Count, MaxEntries);
                for (int i = 0; i < count; i++)
                {
                    var e = entries[i];
                    if (!IsValid(e)) continue;

                    snapshotIndexes.Add(e.OuterIndex);
                    snapshotTypes.Add(e.A);
                    snapshotKeys.Add(e.Key);

                    if (e.EndSec > maxEndSec)
                        maxEndSec = e.EndSec;

                    // 偏差检测 仅记录实际修正 不改变替换逻辑
                    double expected = ExpectedRemaining(e, nowSec);
                    if (expected > 0.0)
                    {
                        double local = AffectCodec.GetRemainingSec(mob, e, fps);
                        if (local > 0.0
                            && Math.Abs(local - expected) > DriftLogThresholdSec)
                        {
                            Logger.Verbose(
                                "Affect duration corrected: netId={NetId} a={A} uniq={UniqId} " +
                                "local={Local:F3} server={Server:F3}",
                                netId, e.A, e.UniqId, local, expected);
                        }
                    }

                    // 开始/结束时间统一由 AffectCodec.Apply 按服务端剩余时长判断
                    active.Add(e);
                }
            }

            // 上一次存在、这次消失的条目 = 服务端已移除
            if (lastKeys.TryGetValue(netId, out var previous))
            {
                foreach (var key in previous)
                {
                    if (snapshotKeys.Contains(key)) continue;

                    Logger.Verbose(
                        "Affect removed by server: netId={NetId} a={A} uniq={UniqId}",
                        netId, key.A, key.UniqId);
                }
            }

            lastKeys[netId] = new HashSet<AffectKey>(snapshotKeys);
            nextExpiry[netId] = maxEndSec;

            if (!serverOwnedIndexes.TryGetValue(netId, out var owned))
            {
                owned = [];
                serverOwnedIndexes[netId] = owned;
            }

            // 旧快照接管过的槽位 + 本次快照槽位 全部以服务端为准重建
            foreach (int idx in owned)
                AffectCodec.ClearIndex(mob, idx);

            foreach (int idx in snapshotIndexes)
                AffectCodec.ClearIndex(mob, idx);

            owned.Clear();
            foreach (int idx in snapshotIndexes)
                owned.Add(idx);

            // 同一 effect 类型只保留服务端快照里的条目
            if (snapshotTypes.Count > 0)
                AffectCodec.RemoveByAffectTypes(mob, snapshotTypes, snapshotIndexes);

            if (active.Count > 0)
                AffectCodec.Apply(mob, active, nowSec, fps);
        }

        /// <summary>
        /// 没有新快照时按权威结束时间清理
        /// </summary>
        public void ExpireIfDue(uint netId, Mob mob, double nowSec)
        {
            if (!double.IsFinite(nowSec)) return;
            if (!nextExpiry.TryGetValue(netId, out double expiry)) return;
            if (expiry <= 0.0 || nowSec < expiry) return;

            if (serverOwnedIndexes.TryGetValue(netId, out var owned))
            {
                foreach (int idx in owned)
                    AffectCodec.ClearIndex(mob, idx);
            }

            serverOwnedIndexes.Remove(netId);
            lastKeys.Remove(netId);
            nextExpiry.Remove(netId);

            Logger.Verbose("Affect snapshot expired: netId={NetId}", netId);
        }

        /// <summary>
        /// 怪物移除时清理其快照记录
        /// </summary>
        public void Remove(uint netId)
        {
            serverOwnedIndexes.Remove(netId);
            lastKeys.Remove(netId);
            nextExpiry.Remove(netId);
        }

        /// <summary>
        /// epoch/关卡清理时重置全部记录
        /// </summary>
        public void Reset()
        {
            serverOwnedIndexes.Clear();
            lastKeys.Clear();
            nextExpiry.Clear();
        }

        private static double ExpectedRemaining(AffectEntry e, double nowSec)
        {
            if (e.ServerTimeSec <= 0.0)
                return e.EndSec - nowSec;

            double age = nowSec - e.ServerTimeSec;
            if (age < 0.0) age = 0.0;
            return e.RemainingSec - age;
        }

        private static bool IsValid(AffectEntry? e)
        {
            if (e == null) return false;
            if (e.OuterIndex < 0 || e.OuterIndex >= MaxOuterIndex) return false;
            if (e.InnerIndex < 0 || e.InnerIndex >= MaxInnerIndex) return false;
            if (!double.IsFinite(e.T) || !double.IsFinite(e.Val)) return false;
            if (!double.IsFinite(e.StartSec) || !double.IsFinite(e.EndSec)) return false;
            if (!double.IsFinite(e.RemainingSec) || !double.IsFinite(e.ServerTimeSec)) return false;
            return e.EndSec > e.StartSec;
        }
    }
}
