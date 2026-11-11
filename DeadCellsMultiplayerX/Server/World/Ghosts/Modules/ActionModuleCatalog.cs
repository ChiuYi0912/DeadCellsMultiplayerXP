using System;
using System.Collections.Generic;
using System.Linq;
using DeadCellsMultiplayerX.Common.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Ghosts.Execution;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.Modules
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

        public static ActionRegistry CreateRegistry(
            IEnumerable<IServerActionModule> modules)
            => new(modules.Select(module => module.Descriptor));
    }
}
