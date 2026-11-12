namespace DeadCellsMultiplayerX.Client.Presentation.Combat;

using dc.en;

/// <summary>
/// 允许指定客户端 mob 执行原生 onDie
/// 预测致死、Ghost 帧致死、服务端确认死亡时临时打开
/// </summary>
internal sealed class CombatDeathGate
{
    private readonly Dictionary<Mob, int> depths = [];

    public bool IsAllowed(Mob mob)
        => mob != null && this.depths.ContainsKey(mob);

    public IDisposable Allow(Mob mob)
    {
        if (mob == null)
        {
            return EmptyScope.Instance;
        }

        this.depths.TryGetValue(mob, out var depth);
        this.depths[mob] = depth + 1;
        return new Scope(this, mob);
    }

    public void Clear() => this.depths.Clear();

    private sealed class Scope(CombatDeathGate gate, Mob mob) : IDisposable
    {
        private readonly CombatDeathGate gate = gate;
        private readonly Mob mob = mob;
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            if (!this.gate.depths.TryGetValue(this.mob, out var depth))
            {
                return;
            }

            if (depth <= 1)
            {
                this.gate.depths.Remove(this.mob);
            }
            else
            {
                this.gate.depths[this.mob] = depth - 1;
            }
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
