using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;
using DeadCellsMultiplayerX.Server.Replication.Entities;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules
{
    /// <summary>
    /// 服务端动作模块的唯一注册点。
    /// 思路：把模块的构造集中到 catalog，ActionExecutor 只负责字典分发与重复检测；
    /// 新增动作只需追加一个 module，不再修改执行器的 switch 或硬编码数组。
    /// </summary>
    internal static class ActionModuleCatalog
    {
        public static IServerActionModule[] CreateDefault(
            ServerEntityRegistry entities,
            Func<long> clock)
        {
            return
            [
                new SkillActionHandler(),
                new WeaponActionHandler(),
                new TalismanActionHandler(),
                new ShieldActionHandler(entities, clock),
            ];
        }
    }
}
