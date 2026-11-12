namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using dc.en;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using Serilog;

/// <summary>
/// 客户端怪物 affect 快照镜像
/// 服务端快照是唯一权威 本地只负责校准时长 清除旧状态并应用快照
/// </summary>
internal sealed class ClientMobAffectState
{
    private const int MAX_ENTRIES = 256;
    private const int MAX_OUTER_INDEX = 256;
    private const int MAX_INNER_INDEX = 256;
    private const double DRIFT_LOG_THRESHOLD_SEC = 0.25;

    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ClientMobAffectState");

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
        if (mob == null || mob.destroyed)
        {
            return;
        }

        if (!double.IsFinite(nowSec))
        {
            return;
        }

        var affects = mob.affects;
        if (affects == null)
        {
            return;
        }

        var baseFps = mob.cd?.baseFps ?? 60.0;
        var fps = baseFps > 0.0 && double.IsFinite(baseFps) ? baseFps : 60.0;

        this.active.Clear();
        this.snapshotIndexes.Clear();
        this.snapshotTypes.Clear();
        this.snapshotKeys.Clear();

        var maxEndSec = 0.0;

        if (entries != null)
        {
            var count = Math.Min(entries.Count, MAX_ENTRIES);
            for (var i = 0; i < count; i++)
            {
                var e = entries[i];
                if (!IsValid(e))
                {
                    continue;
                }

                this.snapshotIndexes.Add(e.OuterIndex);
                this.snapshotTypes.Add(e.A);
                this.snapshotKeys.Add(e.Key);

                if (e.EndSec > maxEndSec)
                {
                    maxEndSec = e.EndSec;
                }

                // 偏差检测 仅记录实际修正 不改变替换逻辑
                var expected = ExpectedRemaining(e, nowSec);
                if (expected > 0.0)
                {
                    var local = AffectCodec.GetRemainingSec(mob, e, fps);
                    if (local > 0.0
                        && Math.Abs(local - expected) > DRIFT_LOG_THRESHOLD_SEC)
                    {
                        Logger.Verbose(
                            "Affect duration corrected: netId={NetId} a={A} uniq={UniqId} " +
                            "local={Local:F3} server={Server:F3}",
                            netId, e.A, e.UniqId, local, expected);
                    }
                }

                // 开始/结束时间统一由 AffectCodec.Apply 按服务端剩余时长判断
                this.active.Add(e);
            }
        }

        // 上一次存在、这次消失的条目 = 服务端已移除
        if (this.lastKeys.TryGetValue(netId, out var previous))
        {
            foreach (var key in previous)
            {
                if (this.snapshotKeys.Contains(key))
                {
                    continue;
                }

                Logger.Verbose(
                    "Affect removed by server: netId={NetId} a={A} uniq={UniqId}",
                    netId, key.A, key.UniqId);
            }
        }

        this.lastKeys[netId] = [.. this.snapshotKeys];
        this.nextExpiry[netId] = maxEndSec;

        if (!this.serverOwnedIndexes.TryGetValue(netId, out var owned))
        {
            owned = [];
            this.serverOwnedIndexes[netId] = owned;
        }

        // 旧快照接管过的槽位 + 本次快照槽位 全部以服务端为准重建
        foreach (var idx in owned)
        {
            AffectCodec.ClearIndex(mob, idx);
        }

        foreach (var idx in this.snapshotIndexes)
        {
            AffectCodec.ClearIndex(mob, idx);
        }

        owned.Clear();
        foreach (var idx in this.snapshotIndexes)
        {
            owned.Add(idx);
        }

        // 同一 effect 类型只保留服务端快照里的条目
        if (this.snapshotTypes.Count > 0)
        {
            AffectCodec.RemoveByAffectTypes(mob, this.snapshotTypes, this.snapshotIndexes);
        }

        if (this.active.Count > 0)
        {
            AffectCodec.Apply(mob, this.active, nowSec, fps);
        }
    }

    /// <summary>
    /// 没有新快照时按权威结束时间清理
    /// </summary>
    public void ExpireIfDue(uint netId, Mob mob, double nowSec)
    {
        if (!double.IsFinite(nowSec))
        {
            return;
        }

        if (!this.nextExpiry.TryGetValue(netId, out var expiry))
        {
            return;
        }

        if (expiry <= 0.0 || nowSec < expiry)
        {
            return;
        }

        if (this.serverOwnedIndexes.TryGetValue(netId, out var owned))
        {
            foreach (var idx in owned)
            {
                AffectCodec.ClearIndex(mob, idx);
            }
        }

        this.serverOwnedIndexes.Remove(netId);
        this.lastKeys.Remove(netId);
        this.nextExpiry.Remove(netId);

        Logger.Verbose("Affect snapshot expired: netId={NetId}", netId);
    }

    /// <summary>
    /// 怪物移除时清理其快照记录
    /// </summary>
    public void Remove(uint netId)
    {
        this.serverOwnedIndexes.Remove(netId);
        this.lastKeys.Remove(netId);
        this.nextExpiry.Remove(netId);
    }

    /// <summary>
    /// epoch/关卡清理时重置全部记录
    /// </summary>
    public void Reset()
    {
        this.serverOwnedIndexes.Clear();
        this.lastKeys.Clear();
        this.nextExpiry.Clear();
    }

    private static double ExpectedRemaining(AffectEntry e, double nowSec)
    {
        if (e.ServerTimeSec <= 0.0)
        {
            return e.EndSec - nowSec;
        }

        var age = nowSec - e.ServerTimeSec;
        if (age < 0.0)
        {
            age = 0.0;
        }

        return e.RemainingSec - age;
    }

    private static bool IsValid(AffectEntry? e)
    {
        if (e == null)
        {
            return false;
        }

        if (e.OuterIndex is < 0 or >= MAX_OUTER_INDEX)
        {
            return false;
        }

        if (e.InnerIndex is < 0 or >= MAX_INNER_INDEX)
        {
            return false;
        }

        if (!double.IsFinite(e.T) || !double.IsFinite(e.Val))
        {
            return false;
        }

        if (!double.IsFinite(e.StartSec) || !double.IsFinite(e.EndSec))
        {
            return false;
        }

        if (!double.IsFinite(e.RemainingSec) || !double.IsFinite(e.ServerTimeSec))
        {
            return false;
        }

        return e.EndSec > e.StartSec;
    }
}
