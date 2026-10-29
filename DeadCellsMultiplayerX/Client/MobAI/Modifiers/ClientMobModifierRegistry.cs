using dc.en;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.MobAI.Events;
using DeadCellsMultiplayerX.Client.MobAI.Modifiers.Zombie;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Client.MobAI.Modifiers
{
    /// <summary>
    /// 客户端 mob 修改器注册器
    /// </summary>
    internal sealed class ClientMobModifierRegistry
    {
        private readonly GuestClientSession session;
        private readonly RemoteWorld world;
        private readonly IClientMobEventBus events;
        private readonly MobModifierRegistry<ClientMobModifierContext> inner = new();

        public ClientMobModifierRegistry(
            GuestClientSession session,
            RemoteWorld world,
            IClientMobEventBus events)
        {
            this.session = session;
            this.world = world;
            this.events = events;

            inner.Register<GameZombie>(
                ctx => new ZombieClientModifier(ctx.Session, ctx.World));
        }

        /// <summary>
        /// 注册一个客户端 mob 修改器工厂
        /// </summary>
        public void Register(
            System.Type mobType,
            Func<ClientMobModifierContext, IMobModifier> factory)
            => inner.Register(mobType, factory);

        /// <summary>
        /// 为客户端 mob 绑定修改器
        /// </summary>
        public bool TryAttach(Mob mob, long tick)
        {
            if (mob == null
                || !world.Mobs.Receiver.TryGetNetId(mob, out uint netId))
                return false;

            var context = new ClientMobModifierContext(
                mob, netId, tick, session, world, events);

            return inner.TryAttach(context);
        }

        /// <summary>
        /// 解绑指定 mob
        /// </summary>
        public void Detach(Mob mob) => inner.Detach(mob);

        /// <summary>
        /// 查询 mob 是否由修改器接管
        /// </summary>
        public bool TryGet(Mob mob, out IMobModifier modifier)
            => inner.TryGet(mob, out modifier);

        /// <summary>
        /// 客户端 AI 锁定拦截；null 表示没有修改器处理
        /// </summary>
        public bool? DispatchAiLocked(Mob mob, long tick)
            => inner.DispatchAiLocked(mob, tick);

        /// <summary>
        /// 客户端每帧更新拦截；返回 true 时调用方跳过默认表现
        /// </summary>
        public bool DispatchClientUpdate(Mob mob, long tick)
            => inner.DispatchClientUpdate(mob, tick);

        /// <summary>
        /// 客户端自定义事件
        /// </summary>
        public void DispatchClientEvent(
            Mob mob, MobModifierEvent evt, long tick)
            => inner.DispatchClientEvent(mob, evt, tick);

        /// <summary>
        /// 清空全部绑定。
        /// </summary>
        public void Clear() => inner.Clear();
    }
}
