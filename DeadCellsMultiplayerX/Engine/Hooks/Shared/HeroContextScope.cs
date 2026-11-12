namespace DeadCellsMultiplayerX.Engine.Hooks.Shared;

using dc.en;
using dc.pr;

/// <summary>
/// 临时切换全局 <see cref="Game.hero"/> 的作用域。
/// 统一处理嵌套、异常与 previous 为 null 的恢复；
/// 同时把 viewport 临时扩展为覆盖全图，供依赖相机距离的原版逻辑使用。
/// </summary>
internal sealed class HeroContextScope : IDisposable
{
    [ThreadStatic]
    private static int depth;

    [ThreadStatic]
    private static Game? activeGame;

    [ThreadStatic]
    private static Hero? previousHero;

    [ThreadStatic]
    private static ViewportSnapshot viewportSnapshot;

    private readonly bool owner;
    private bool disposed;

    private HeroContextScope(bool owner) => this.owner = owner;

    /// <summary>
    /// 在指定 Hero 上下文中执行；game 为空或已在嵌套作用域内时直接返回空句柄。
    /// </summary>
    public static IDisposable Enter(Game? game, Hero? hero)
    {
        if (game == null || depth > 0)
        {
            return new HeroContextScope(false);
        }

        activeGame = game;
        previousHero = game.hero;

        if (hero != null && !ReferenceEquals(previousHero, hero))
        {
            game.hero = hero;
        }

        viewportSnapshot = ViewportSnapshot.Capture(hero);
        viewportSnapshot.ApplyFullMap(hero);

        depth = 1;
        return new HeroContextScope(true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        if (!this.owner || depth == 0)
        {
            return;
        }

        depth = 0;

        viewportSnapshot.Restore();
        viewportSnapshot = default;

        activeGame?.hero = previousHero;

        activeGame = null;
        previousHero = null;
    }

    private struct ViewportSnapshot
    {
        private dc.Viewport? viewport;
        private double x;
        private double y;
        private int wid;
        private int hei;
        private bool captured;

        public static ViewportSnapshot Capture(Hero? hero)
        {
            var viewport = hero?._level?.viewport;
            if (viewport == null)
            {
                return default;
            }

            return new ViewportSnapshot
            {
                viewport = viewport,
                x = viewport.x,
                y = viewport.y,
                wid = viewport.wid,
                hei = viewport.hei,
                captured = true,
            };
        }

        public readonly void ApplyFullMap(Hero? hero)
        {
            if (!this.captured || this.viewport == null)
            {
                return;
            }

            var map = hero?._level?.map;
            if (map == null)
            {
                return;
            }

            // x/y 是镜头中心，wid/hei 是像素尺寸
            this.viewport.x = map.wid * 12.0;
            this.viewport.y = map.hei * 12.0;
            this.viewport.wid = map.wid * 24;
            this.viewport.hei = map.hei * 24;
        }

        public readonly void Restore()
        {
            if (!this.captured || this.viewport == null)
            {
                return;
            }

            this.viewport.x = this.x;
            this.viewport.y = this.y;
            this.viewport.wid = this.wid;
            this.viewport.hei = this.hei;
        }
    }
}
