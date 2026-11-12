namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior.Zombie;

using dc;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.Session;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using Serilog;
using GameZombie = dc.en.mob.Zombie;

/// <summary>
/// Zombie 服务端修改器占位实现
/// 当前只验证注册、依赖注入、InitHook 和 hook 分发；
/// </summary>
internal sealed class ZombieServerModifier(
    ServerSession session,
    ServerTeamManager teams,
    IServerMobAttackRouter attacks) : MobModifierBase<GameZombie>
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ZombieServerModifier");

    private readonly ServerSession session = session;
    private readonly ServerTeamManager teams = teams;
    private readonly IServerMobAttackRouter attacks = attacks;

    /// <inheritdoc />
    public override void InitHook(IMobHookRegistry hooks)
    {
        if (hooks is IServerMobHooks serverHooks)
        {
            serverHooks.OnSkillPrepare(this.SandSkills);
        }
    }

    private void SandSkills(MobHookContext context, OldSkill skill, Entity target, int? data)
    {
        if (!this.teams.TryGet(target, out var combatant))
        {
            return;
        }

        if (this.session.Entities.TryGetNetId(
                context.Mob, out var netId))
        {
            this.attacks.SubmitSkill(
                new MobModifierEvent
                {
                    Epoch = this.session.Broadcaster.Epoch,
                    NetId = netId,
                    EntityKind = EntityKind.Mob,
                    Kind = MobModifierEventKind.ActionStarted,
                    TargetGuid = combatant.Guid,
                    SkillId = skill.id.ToString(),
                    SkillRatio = data,
                    ServerTimeMs = this.session.CurrentTimeStamp,
                    LevelId = combatant.Level?.map?.id?.ToString()
                        ?? string.Empty,
                    SubLevelIndex = combatant.Level
                        ?.GetSubLevelIndex() ?? -1,
                });
        }
    }

    /// <inheritdoc />
    public override void Attach(GameZombie mob, IMobModifierContext context)
    {
    }

    /// <inheritdoc />
    public override void Detach(GameZombie mob)
    {
    }
}
