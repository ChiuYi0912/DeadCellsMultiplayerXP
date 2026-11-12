namespace DeadCellsMultiplayerX.Server.Replication.Entities
{
    /// <summary>
    /// 全局单调 NetId 分配器
    /// 只在世界重置时清零，不按关卡重置
    /// </summary>
    internal sealed class NetIdAllocator
    {
        private uint next = 1;

        public uint Next() => next++;

        public void Reset() => next = 1;
    }
}
