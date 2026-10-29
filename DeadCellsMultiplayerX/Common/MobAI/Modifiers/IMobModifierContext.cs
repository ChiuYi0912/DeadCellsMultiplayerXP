using System;
using dc.en;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 修改器绑定上下文
    /// 注册器在创建修改器前构造，并作为依赖注入的轻量入口
    /// </summary>
    public interface IMobModifierContext
    {
        /// <summary>
        /// 当前 mob 实体
        /// </summary>
        Mob Mob { get; }

        /// <summary>
        /// 服务端分配的 NetId；客户端从 MobSpawn 映射得到
        /// </summary>
        uint NetId { get; }

        /// <summary>
        /// 当前 mob 的 CLR 类型
        /// </summary>
        Type MobType { get; }

        /// <summary>
        /// 绑定时的时间戳
        /// </summary>
        long Tick { get; }

        /// <summary>
        /// 查询服务端/客户端提供的依赖
        /// </summary>
        T? GetService<T>() where T : class;
    }
}
