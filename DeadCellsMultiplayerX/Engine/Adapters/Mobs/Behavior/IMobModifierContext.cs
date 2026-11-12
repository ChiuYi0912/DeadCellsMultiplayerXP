namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

using dc.en;

/// <summary>
/// 修改器绑定上下文
/// 注册器在创建修改器前构造，并作为依赖注入的轻量入口
/// </summary>
public interface IMobModifierContext
{
    /// <summary>
    /// 当前 mob 实体
    /// </summary>
    public Mob Mob { get; }

    /// <summary>
    /// 服务端分配的 NetId；客户端从 EntitySpawn 映射得到
    /// </summary>
    public uint NetId { get; }

    /// <summary>
    /// 当前 mob 的 CLR 类型
    /// </summary>
    public Type MobType { get; }

    /// <summary>
    /// 绑定时的时间戳
    /// </summary>
    public long Tick { get; }

    /// <summary>
    /// 查询服务端/客户端提供的依赖
    /// </summary>
    public T GetService<T>() where T : class;
}
