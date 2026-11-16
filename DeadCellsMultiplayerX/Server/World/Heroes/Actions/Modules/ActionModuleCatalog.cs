namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;

using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

/// <summary>
/// 服务端动作模块的唯一注册点。
/// </summary>
internal static class ActionModuleCatalog
{
    public static IHeroActionModule[] CreateDefault(
        Func<long> clock) => [
            new SkillActionHandler(),
            new WeaponActionHandler(),
            new ShieldActionHandler(clock),
        ];
}
