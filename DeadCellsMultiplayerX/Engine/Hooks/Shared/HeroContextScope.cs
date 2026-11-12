using dc.en;
using dc.pr;

namespace DeadCellsMultiplayerX.Engine.Hooks.Shared
{
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

        private HeroContextScope(bool owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// 在指定 Hero 上下文中执行；game 为空或已在嵌套作用域内时直接返回空句柄。
        /// </summary>
        public static IDisposable Enter(Game? game, Hero? hero)
        {
            if (game == null || depth > 0)
                return new HeroContextScope(false);

            activeGame = game;
            previousHero = game.hero;

            if (hero != null && !ReferenceEquals(previousHero, hero))
                game.hero = hero;

            viewportSnapshot = ViewportSnapshot.Capture(hero);
            viewportSnapshot.ApplyFullMap(hero);

            depth = 1;
            return new HeroContextScope(true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (!owner || depth == 0)
                return;

            depth = 0;

            viewportSnapshot.Restore();
            viewportSnapshot = default;

            if (activeGame != null)
                activeGame.hero = previousHero!;

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
                    return default;

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

            public void ApplyFullMap(Hero? hero)
            {
                if (!captured || viewport == null)
                    return;

                var map = hero?._level?.map;
                if (map == null)
                    return;

                // x/y 是镜头中心，wid/hei 是像素尺寸
                viewport.x = map.wid * 12.0;
                viewport.y = map.hei * 12.0;
                viewport.wid = map.wid * 24;
                viewport.hei = map.hei * 24;
            }

            public void Restore()
            {
                if (!captured || viewport == null)
                    return;

                viewport.x = x;
                viewport.y = y;
                viewport.wid = wid;
                viewport.hei = hei;
            }
        }
    }
}
