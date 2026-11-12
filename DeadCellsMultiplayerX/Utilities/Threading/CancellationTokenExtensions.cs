namespace DeadCellsMultiplayerX.Utilities.Threading
{
    /// <summary>
    /// CancellationToken 组合扩展。
    /// </summary>
    internal static class CancellationTokenExtensions
    {
        /// <summary>
        /// 任一 token 取消即取消；全部不可取消时返回 None。
        /// </summary>
        public static CancellationToken Combine(
            this CancellationToken first,
            params CancellationToken[] others)
        {
            var active = new List<CancellationToken>(1 + (others?.Length ?? 0));

            if (first.CanBeCanceled)
                active.Add(first);

            if (others != null)
            {
                foreach (var token in others)
                {
                    if (token.CanBeCanceled && !active.Contains(token))
                        active.Add(token);
                }
            }

            return active.Count switch
            {
                0 => CancellationToken.None,
                1 => active[0],
                _ => CancellationTokenSource
                    .CreateLinkedTokenSource(active.ToArray())
                    .Token,
            };
        }
    }
}
