using System;
using dc.en;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 一次 hook 调用的上下文
    /// </summary>
    public sealed class MobHookContext
    {
        /// <summary>
        /// 创建 hook 上下文
        /// </summary>
        public MobHookContext(IMobModifierContext modifierContext, long tick)
        {
            ModifierContext = modifierContext;
            Mob = modifierContext.Mob;
            NetId = modifierContext.NetId;
            MobType = modifierContext.MobType;
            Tick = tick;
        }

        /// <summary>
        /// 当前 mob
        /// </summary>
        public Mob Mob { get; }

        /// <summary>
        /// 当前 mob 的 NetId
        /// </summary>
        public uint NetId { get; }

        /// <summary>
        /// 当前 mob 的 CLR 类型
        /// </summary>
        public Type MobType { get; }

        /// <summary>
        /// 当前 hook 触发时间
        /// </summary>
        public long Tick { get; }

        /// <summary>
        /// 修改器绑定上下文，可继续取服务端/客户端依赖
        /// </summary>
        public IMobModifierContext ModifierContext { get; }
    }
}
