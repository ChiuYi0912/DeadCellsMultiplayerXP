namespace DeadCellsMultiplayerX.Client.Presentation.Combat;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Mobs.Replication;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Time;
using Serilog;

/// <summary>
/// 本地武器命中预测
/// 致死命中走 orig,非致死命中生命预测
/// 服务端确认存活时由 ClientMobReplicator 重建 mob
/// </summary>
internal sealed class LocalCombatPrediction(GuestClientSession session)
{
    private const long TIMEOUT_MS = 500;
    private const long CONFIRMED_LIFE_FALLBACK_MS = 2000;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "LocalCombatPrediction");

    private readonly GuestClientSession session = session;
    private readonly Dictionary<uint, List<PredictedMob>> pendingByMob = [];
    private readonly Stack<PredictionBatch> activeBatches = [];
    private readonly Dictionary<long, PredictionBatch> batchesByToken = [];
    private readonly Dictionary<uint, ConfirmedLife> confirmedLife = [];
    private long nextToken;

    /// <summary>
    /// 当前存在未确认预测的 mob 数量
    /// </summary>
    public int PendingMobCount => this.pendingByMob.Count;

    private sealed class PredictionBatch
    {
        public long Token { get; set; }
        public long ActionId { get; set; }
        public long ExpiresAtMs { get; set; }
        public Dictionary<uint, PredictedMob> Mobs { get; } = [];
    }

    private sealed class PredictedMob(Mob mob, PredictionBatch batch)
    {
        public Mob Mob { get; } = mob;
        public PredictionBatch Batch { get; } = batch;
        public int Life { get; set; }
        public bool Dead { get; set; }
        public bool Confirmed { get; set; }
        public bool ServerDead { get; set; }
        public long ConfirmServerTimeMs { get; set; }
    }

    private sealed class ConfirmedLife
    {
        public int Life { get; set; }
        public long ServerTimeMs { get; set; }
    }

    public long BeginWeapon()
    {
        var token = ++this.nextToken;

        var batch = new PredictionBatch
        {
            Token = token,
            ExpiresAtMs = this.session.SyncedTimeMs + TIMEOUT_MS,
        };

        this.activeBatches.Push(batch);
        this.batchesByToken[token] = batch;
        return token;
    }

    public void EndWeapon()
    {
        if (this.activeBatches.Count > 0)
        {
            this.activeBatches.Pop();
        }
    }

    public void CommitWeapon(long token, long actionId)
    {
        if (!this.batchesByToken.TryGetValue(token, out var batch))
        {
            return;
        }

        batch.ActionId = actionId;

        if (batch.Mobs.Count == 0)
        {
            this.batchesByToken.Remove(token);
        }
    }

    /// <summary>
    /// Ghost 帧结算写入的权威生命
    /// </summary>
    public void ApplyConfirmedLife(
        uint netId,
        int life,
        long serverTimeMs) => this.confirmedLife[netId] = new ConfirmedLife
        {
            Life = life,
            ServerTimeMs = serverTimeMs,
        };

    /// <summary>
    /// 计算本帧显示生命；不处理可见性
    /// </summary>
    public int ResolveLife(
        uint netId,
        int serverLife,
        long renderTimeMs)
    {
        var life = serverLife;

        if (this.confirmedLife.TryGetValue(netId, out var confirmed))
        {
            if (renderTimeMs < confirmed.ServerTimeMs
                + SnapshotTiming.INTERPOLATION_DELAY_MS)
            {
                // Ghost 帧结果优先于 100ms 插值后的旧生命
                life = confirmed.Life;
            }
            else
            {
                this.confirmedLife.Remove(netId);
            }
        }

        if (!this.pendingByMob.TryGetValue(netId, out var list)
            || list.Count == 0)
        {
            return life;
        }

        for (var i = list.Count - 1; i >= 0; i--)
        {
            var predicted = list[i];

            if (predicted.Confirmed
                && !predicted.ServerDead
                && renderTimeMs >= predicted.ConfirmServerTimeMs
                + SnapshotTiming.INTERPOLATION_DELAY_MS)
            {
                list.RemoveAt(i);
                predicted.Batch.Mobs.Remove(netId);

                if (predicted.Batch.Mobs.Count == 0)
                {
                    this.batchesByToken.Remove(predicted.Batch.Token);
                }

                continue;
            }

            life = Math.Min(life, predicted.Life);
        }

        if (list.Count == 0)
        {
            this.pendingByMob.Remove(netId);
        }

        return life;
    }

    public void Tick(long nowMs)
    {
        List<long> remove = [];

        foreach (var batch in this.batchesByToken.Values)
        {
            var unconfirmedTimeout =
                batch.Mobs.Count > 0
                && batch.Mobs.Values.Any(p => !p.Confirmed)
                && nowMs >= batch.ExpiresAtMs;

            var uncommittedTimeout =
                batch.ActionId == 0 && nowMs >= batch.ExpiresAtMs;

            var confirmedAliveTimeout =
                batch.Mobs.Count > 0
                && batch.Mobs.Values.All(p => p.Confirmed && !p.ServerDead)
                && nowMs >= batch.ExpiresAtMs + TIMEOUT_MS;

            if (unconfirmedTimeout
                || uncommittedTimeout
                || confirmedAliveTimeout)
            {
                remove.Add(batch.Token);
            }
        }

        foreach (var token in remove)
        {
            if (this.batchesByToken.TryGetValue(token, out var batch))
            {
                this.RemoveBatch(batch);
            }
        }

        List<uint> expiredConfirmed = [];
        foreach (var (netId, confirmed) in this.confirmedLife)
        {
            if (nowMs >= confirmed.ServerTimeMs + CONFIRMED_LIFE_FALLBACK_MS)
            {
                expiredConfirmed.Add(netId);
            }
        }

        foreach (var netId in expiredConfirmed)
        {
            this.confirmedLife.Remove(netId);
        }
    }

    /// <summary>
    /// 确认本地 caster 的 HeroActionEvent
    /// 必须在 ClientMobReplicator 的 receiver.Remove 之前调用
    /// </summary>
    public void ConfirmFrame(WorldFrame frame)
    {
        if (frame?.Actions == null || frame.Actions.Count == 0)
        {
            return;
        }

        if (this.session.ClientWorld == null)
        {
            return;
        }

        var localGuid = this.session.Client.Guid;

        foreach (var evt in frame.Actions)
        {
            if (evt == null)
            {
                continue;
            }

            if (evt.CasterGuid != localGuid)
            {
                continue;
            }

            if (evt.Epoch != frame.Epoch)
            {
                continue;
            }

            List<PredictionBatch> matches = [];
            foreach (var batch in this.batchesByToken.Values)
            {
                if (batch.ActionId == evt.ActionId)
                {
                    matches.Add(batch);
                }
            }

            foreach (var batch in matches)
            {
                this.ConfirmBatch(batch, frame, evt);
            }
        }
    }

    public void ConfirmDespawn(uint netId)
    {
        if (!this.pendingByMob.TryGetValue(netId, out var list))
        {
            return;
        }

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
        if (!this.pendingByMob.Remove(netId, out var list))
        {
            this.confirmedLife.Remove(netId);
            return;
        }

        foreach (var predicted in list)
        {
            predicted.Confirmed = true;
            predicted.ServerDead = true;
        }

        List<PredictionBatch> empty = [];
        foreach (var batch in this.batchesByToken.Values)
        {
            if (batch.Mobs.Remove(netId) && batch.Mobs.Count == 0)
            {
                empty.Add(batch);
            }
        }

        foreach (var batch in empty)
        {
            this.batchesByToken.Remove(batch.Token);
        }

        this.confirmedLife.Remove(netId);
    }

    /// <summary>
    /// 重建新实体后清除该 NetId 的旧预测/覆盖
    /// </summary>
    public void ClearNetId(uint netId)
    {
        this.pendingByMob.Remove(netId);
        this.confirmedLife.Remove(netId);

        List<PredictionBatch> empty = [];
        foreach (var batch in this.batchesByToken.Values)
        {
            if (batch.Mobs.Remove(netId) && batch.Mobs.Count == 0)
            {
                empty.Add(batch);
            }
        }

        foreach (var batch in empty)
        {
            this.batchesByToken.Remove(batch.Token);
        }
    }

    /// <summary>
    /// 是否还有未结算的本地死亡预测；未确认前不要重建实体
    /// </summary>
    public bool HasPendingDeathPrediction(uint netId) => this.pendingByMob.TryGetValue(netId, out var list)
            && list.Any(p => p.Dead || p.ServerDead);

    public void Clear()
    {
        this.pendingByMob.Clear();
        this.activeBatches.Clear();
        this.batchesByToken.Clear();
        this.confirmedLife.Clear();
    }

    private void ConfirmBatch(
        PredictionBatch batch,
        WorldFrame frame,
        HeroActionEvent evt)
    {
        foreach (var entry in batch.Mobs.ToList())
        {
            var netId = entry.Key;
            var predicted = entry.Value;

            EntityDynamic? current =
                MobFrameQueries.FindMob(frame, netId);

            var serverDead = MobFrameQueries.ContainsDespawn(frame, netId)
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
                {
                    this.session.ForceNativeDeath(predicted.Mob);
                }

                continue;
            }

            // 服务端认为还活着但本地预测了死亡：交给重建流程
            if (predicted.Dead)
            {
                this.RemovePrediction(netId, predicted);
                Logger.Debug(
                    "Prediction death rolled back: netId={NetId} action={ActionId}",
                    netId, evt.ActionId);
            }
        }
    }

    private void RemovePrediction(uint netId, PredictedMob predicted)
    {
        if (this.pendingByMob.TryGetValue(netId, out var list))
        {
            list.Remove(predicted);
            if (list.Count == 0)
            {
                this.pendingByMob.Remove(netId);
            }
        }

        predicted.Batch.Mobs.Remove(netId);

        if (predicted.Batch.Mobs.Count == 0)
        {
            this.batchesByToken.Remove(predicted.Batch.Token);
        }
    }

    private void RemoveBatch(PredictionBatch batch)
    {
        foreach (var (netId, predicted) in batch.Mobs)
        {
            if (!this.pendingByMob.TryGetValue(netId, out var list))
            {
                continue;
            }

            list.Remove(predicted);

            if (list.Count == 0)
            {
                this.pendingByMob.Remove(netId);
            }
        }

        this.batchesByToken.Remove(batch.Token);
    }
}
