using System;
using System.Collections.Generic;
using System.Linq;
using DeadCellsMultiplayerX.Common.Data.Ghosts;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.World
{
    /// <summary>
    /// 服务端 Ghost 世界
    /// </summary>
    internal sealed class WorldState
    {
        private readonly Dictionary<string, Actor> actors = new(StringComparer.Ordinal);

        private readonly EventJournal events = new();

        /// <summary>
        /// 当前动作 epoch；未开始时为 -1
        /// </summary>
        public int Epoch { get; private set; } = -1;

        /// <summary>
        /// 实际持有 GhostHero 的数量
        /// </summary>
        public int Count => actors.Values.Count(actor => actor.Ghost != null);

        /// <summary>
        /// 全部客户端聚合，供 spawn 同步遍历
        /// </summary>
        public IReadOnlyCollection<Actor> Actors => actors.Values;

        /// <summary>
        /// 按 guid 查找
        /// </summary>
        public Actor? Find(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;

            return actors.TryGetValue(guid, out var actor) ? actor : null;
        }

        /// <summary>
        /// 获取或创建客户端聚合
        /// </summary>
        public Actor GetOrCreate(string guid)
        {
            if (actors.TryGetValue(guid, out var actor))
                return actor;

            actor = new Actor(guid);
            actors[guid] = actor;
            return actor;
        }

        /// <summary>
        /// 开始新 epoch
        /// </summary>
        public void BeginEpoch(int epoch)
        {
            Epoch = epoch;
            Reset();
        }

        /// <summary>
        /// 清空 GhostHero 与事件
        /// </summary>
        public void Reset()
        {
            foreach (var actor in actors.Values)
                actor.Dispose();

            actors.Clear();
            events.Clear();
        }

        /// <summary>
        /// 断线清理
        /// </summary>
        public void RemoveGuest(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;

            if (actors.Remove(guid, out var actor))
                actor.Dispose();
        }

        /// <summary>
        /// 记录一次已执行成功的事件
        /// </summary>
        public void RecordEvent(HeroActionEvent actionEvent)
            => events.Record(actionEvent);

        /// <summary>
        /// 取出本帧全部事件
        /// </summary>
        public List<HeroActionEvent> DrainEvents()
            => events.Drain();
    }
}
