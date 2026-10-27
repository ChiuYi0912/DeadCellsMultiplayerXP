using System;
using System.Collections.Generic;
using dc.en;

namespace DeadCellsMultiplayerX.Client.Guest.Ghost
{
    /// <summary>
    /// 允许指定客户端 mob 执行原生 onDie
    /// 预测致死、Ghost 帧致死、服务端确认死亡时临时打开
    /// </summary>
    internal sealed class CombatDeathGate
    {
        private readonly Dictionary<Mob, int> depths = [];

        public bool IsAllowed(Mob mob)
            => mob != null && depths.ContainsKey(mob);

        public IDisposable Allow(Mob mob)
        {
            if (mob == null)
                return EmptyScope.Instance;

            depths.TryGetValue(mob, out int depth);
            depths[mob] = depth + 1;
            return new Scope(this, mob);
        }

        public void Clear() => depths.Clear();

        private sealed class Scope : IDisposable
        {
            private readonly CombatDeathGate gate;
            private readonly Mob mob;
            private bool disposed;

            public Scope(CombatDeathGate gate, Mob mob)
            {
                this.gate = gate;
                this.mob = mob;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                if (!gate.depths.TryGetValue(mob, out int depth))
                    return;

                if (depth <= 1)
                    gate.depths.Remove(mob);
                else
                    gate.depths[mob] = depth - 1;
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static readonly EmptyScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
