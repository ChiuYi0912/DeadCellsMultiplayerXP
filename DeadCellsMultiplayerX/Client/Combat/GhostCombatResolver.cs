using System;
using System.Collections.Generic;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Combat
{
    /// <summary>
    /// 其他玩家 ghost 武器命中的结算
    /// 服务端事件已经确认，使用同一帧 MobDynamic 的生命和死亡
    /// </summary>
    internal sealed class GhostCombatResolver
    {
        private readonly GuestClientSession session;
        private readonly Stack<Context> active = [];

        public GhostCombatResolver(GuestClientSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// 在一次 ghost 武器重放前建立帧上下文
        /// </summary>
        public IDisposable Begin(WorldFrame frame, HeroActionEvent evt)
        {
            var context = new Context(frame, evt);
            active.Push(context);
            return new Scope(this, context);
        }

        /// <summary>
        /// 处理 ghost 武器命中；返回 true 表示已结算
        /// </summary>
        public bool TryHandleHit(AttackData atk, Mob mob)
        {
            if (active.Count == 0)
                return false;

            if (mob == null || mob.destroyed)
                return true;

            var world = session.RemoteWorld;
            if (world == null
                || !world.Mobs.Receiver.TryGetNetId(mob, out uint netId))
            {
                return false;
            }

            var context = active.Peek();

            bool despawned = context.Despawned.Contains(netId);
            context.Mobs.TryGetValue(netId, out var dynamic);

            if (despawned || (dynamic != null && dynamic.Life <= 0))
            {
                session.ForceNativeDeath(mob);
                return true;
            }

            if (dynamic != null)
            {
                mob.life = dynamic.Life;
                mob.maxLife = dynamic.MaxLife;

                session.Prediction.ApplyConfirmedLife(
                    netId, dynamic.Life, context.FrameTimeMs);

                session.PlayHitVisual(atk, mob);
                return true;
            }

            // 该 NetId 尚未出现在本帧 Mobs 中：只做表现，等后续帧结算
            session.PlayHitVisual(atk, mob);
            return true;
        }

        private void End(Context context)
        {
            if (active.Count > 0 && ReferenceEquals(active.Peek(), context))
                active.Pop();
        }

        private sealed class Context
        {
            public Context(WorldFrame frame, HeroActionEvent evt)
            {
                FrameTimeMs = (long)Math.Round(frame.ServerTimeMs);

                if (frame.Mobs != null)
                {
                    foreach (var mob in frame.Mobs)
                    {
                        if (mob != null)
                            Mobs[mob.NetId] = mob;
                    }
                }

                if (frame.DespawnedMobs != null)
                {
                    foreach (var netId in frame.DespawnedMobs)
                        Despawned.Add(netId);
                }
            }

            public long FrameTimeMs { get; }
            public Dictionary<uint, MobDynamic> Mobs { get; } = [];
            public HashSet<uint> Despawned { get; } = [];
        }

        private sealed class Scope : IDisposable
        {
            private readonly GhostCombatResolver owner;
            private readonly Context context;
            private bool disposed;

            public Scope(GhostCombatResolver owner, Context context)
            {
                this.owner = owner;
                this.context = context;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                owner.End(context);
            }
        }
    }
}
