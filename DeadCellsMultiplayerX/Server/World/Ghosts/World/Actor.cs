using System;
using dc.pr;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.World
{
    /// <summary>
    /// 一个客户端的 Ghost 聚合
    /// </summary>
    internal sealed class Actor : IDisposable
    {
        private uint? appliedSpawnVersion;

        public Actor(string ownerGuid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerGuid);
            OwnerGuid = ownerGuid;
        }

        /// <summary>
        /// 客户端 id
        /// </summary>
        public string OwnerGuid { get; }

        /// <summary>
        /// 当前服务端代表实体,创建失败时为空
        /// </summary>
        public GhostHero? Ghost { get; private set; }

        /// <summary>
        /// ActionId 去重状态
        /// </summary>
        public Admission Admission { get; } = new();

        /// <summary>
        /// 复用同关卡 GhostHero，关卡变化时原地迁移
        /// </summary>
        /// <returns>是否创建了新的 GhostHero</returns>
        public bool EnsureGhost(
            Game game,
            Level level,
            int cx = 0,
            int cy = 0)
        {
            if (Ghost != null)
            {
                if (Ghost.MatchesLevel(level))
                    return false;

                if (Ghost.IsAlive)
                {
                    Ghost.Relocate(level, cx, cy);
                    return false;
                }

                Ghost.Dispose();
                Ghost = null;
            }

            appliedSpawnVersion = null;
            Ghost = GhostHero.Create(
                game, level, OwnerGuid, true, cx, cy);
            return Ghost != null;
        }

        /// <summary>
        /// 判断 spawn 版本是否尚未应用
        /// </summary>
        public bool NeedsSpawn(uint version)
            => appliedSpawnVersion != version;

        /// <summary>
        /// 记录已应用的 spawn 版本
        /// </summary>
        public void MarkSpawnApplied(uint version)
            => appliedSpawnVersion = version;

        public void Dispose()
        {
            Ghost?.Dispose();
            Ghost = null;
        }
    }

    /// <summary>
    /// 单个客户端的 ActionId 去重状态
    /// </summary>
    internal sealed class Admission
    {
        private long lastActionId;

        /// <summary>
        /// 客户端传入的递增ActionId
        /// </summary>
        public bool IsNewAction(long actionId)
            => actionId > lastActionId;

        /// <summary>
        /// GhostHero 创建成功后写入 ActionId 水位
        /// </summary>
        public void Commit(long actionId)
        {
            lastActionId = actionId;
        }
    }
}
