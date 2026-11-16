namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

/// <summary>
/// 显式的“只表现”作用域。
/// 作用域内的命中不得进入权威伤害路径；客户端战斗 Hook 只播放受击表现。
/// </summary>
internal static class HitPresentationScope
{
    [ThreadStatic]
    private static int depth;

    public static bool IsActive => depth > 0;

    public static IDisposable Begin()
    {
        depth++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            if (depth > 0)
            {
                depth--;
            }
        }
    }
}
