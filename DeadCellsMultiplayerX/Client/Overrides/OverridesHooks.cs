namespace DeadCellsMultiplayerX.Client.Overrides;

using DeadCellsMultiplayerX.Client.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Client.Overrides.MainSkills;
using DeadCellsMultiplayerX.Client.Overrides.Weapons;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using DeadCellsMultiplayerX.Engine.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Engine.Overrides.MainSkills;
using DeadCellsMultiplayerX.Engine.Overrides.Weapons;

internal static class OverridesHooks
{
    public static IDisposable Install()
    {
        var hooks = new HookGroup();

        MultiplayerWeaponsHooks.Register(hooks);
        ClientWeaponsHooks.Register(hooks);

        MultiplayerMainSkillsHooks.Register(hooks);
        ClientMainSkillsHooks.Register(hooks);

        MultiplayerActiveSkillsHooks.Register(hooks);
        ClientActiveSkillsHooks.Register(hooks);

        return hooks;
    }
}
