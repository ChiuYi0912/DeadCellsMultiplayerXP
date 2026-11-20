namespace DeadCellsMultiplayerX.Server.Overrides;

using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using DeadCellsMultiplayerX.Engine.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Engine.Overrides.MainSkills;
using DeadCellsMultiplayerX.Engine.Overrides.Weapons;
using DeadCellsMultiplayerX.Server.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Server.Overrides.MainSkills;
using DeadCellsMultiplayerX.Server.Overrides.Weapons;

internal static class ServerOverridesHooks
{
    public static IDisposable Install()
    {
        var hooks = new HookGroup();

        MultiplayerWeaponsHooks.Register(hooks);
        ServerWeaponsHooks.Register(hooks);

        MultiplayerMainSkillsHooks.Register(hooks);
        ServerMainSkillsHooks.Register(hooks);

        MultiplayerActiveSkillsHooks.Register(hooks);
        ServerActiveSkillsHooks.Register(hooks);

        return hooks;
    }
}
