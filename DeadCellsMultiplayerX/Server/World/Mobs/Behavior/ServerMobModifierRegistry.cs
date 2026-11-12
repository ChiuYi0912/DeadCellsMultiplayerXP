namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior;

using dc;
using dc.en;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.Session;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior.Zombie;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using GameZombie = dc.en.mob.Zombie;

/// <summary>
/// 服务端 mob 修改器注册器。
/// 负责构造上下文、执行 InitHook、完成依赖注入并维护生命周期
/// </summary>
internal sealed class ServerMobModifierRegistry
{
    private readonly ServerSession session;
    private readonly ServerTeamManager teams;
    private readonly IServerMobAttackRouter attacks;
    private readonly IServerMobEventOutbox events;
    private readonly MobModifierRegistry<ServerMobModifierContext> inner = new();

    public ServerMobModifierRegistry(
        ServerSession session,
        ServerTeamManager teams,
        IServerMobAttackRouter attacks,
        IServerMobEventOutbox events)
    {
        this.session = session;
        this.teams = teams;
        this.attacks = attacks;
        this.events = events;

        this.inner.Register<GameZombie>(
            ctx => new ZombieServerModifier(
                ctx.Session, ctx.Teams, ctx.Attacks));
    }

    /// <summary>
    /// 注册一个服务端 mob 修改器工厂
    /// </summary>
    public void Register(
        System.Type mobType,
        Func<ServerMobModifierContext, IMobModifier> factory)
        => this.inner.Register(mobType, factory);

    /// <summary>
    /// 为服务端 mob 绑定修改器
    /// </summary>
    public bool TryAttach(Mob mob, long tick)
    {
        if (mob == null
            || !this.session.Entities.TryGetNetId(mob, out var netId))
        {
            return false;
        }

        var context = new ServerMobModifierContext(
            mob, netId, tick,
            this.session, this.teams, this.attacks, this.events);

        return this.inner.TryAttach(context);
    }

    /// <summary>
    /// 解绑指定 mob
    /// </summary>
    public void Detach(Mob mob) => this.inner.Detach(mob);

    /// <summary>
    /// 查询 mob 是否由修改器接管
    /// </summary>
    public bool TryGet(Mob mob, out IMobModifier modifier)
        => this.inner.TryGet(mob, out modifier);

    public bool DispatchBehaviourAi(Mob mob, long tick)
        => this.inner.DispatchBehaviourAi(mob, tick);

    public bool DispatchFixedUpdateHandled(Mob mob, long tick)
        => this.inner.DispatchFixedUpdateHandled(mob, tick);

    public void DispatchPreUpdate(Mob mob, long tick)
        => this.inner.DispatchPreUpdate(mob, tick);

    public void DispatchFixedUpdate(Mob mob, long tick)
        => this.inner.DispatchFixedUpdate(mob, tick);

    public void DispatchPostUpdate(Mob mob, long tick)
        => this.inner.DispatchPostUpdate(mob, tick);

    /// <summary>
    /// 技能准备：注册过修改器的 mob 交给修改器处理；
    /// 其余 mob 统一收集并转发给客户端播放
    /// </summary>
    public void DispatchSkillPrepare(Mob mob, OldSkill skill, Entity target, int? data, long tick)
    {
        if (mob == null || mob.destroyed || skill == null || skill.destroyed)
        {
            return;
        }

        if (this.inner.TryGet(mob, out _))
        {
            this.inner.DispatchSkillPrepare(mob, skill, target, data, tick);
            return;
        }

        this.CollectSkillPrepare(mob, skill, target, data, tick);
    }

    /// <summary>
    /// 未注册修改器的 mob 统一收集技能准备事件
    /// </summary>
    private void CollectSkillPrepare(
        Mob mob, OldSkill skill, Entity target, int? data, long tick)
    {
        if (!this.session.Entities.TryGetNetId(mob, out var netId))
        {
            return;
        }

        var targetGuid = string.Empty;
        if (this.teams.TryGet(target, out var combatant))
        {
            targetGuid = combatant.Guid;
        }

        var level = mob._level;

        this.attacks.SubmitSkill(new MobModifierEvent
        {
            Epoch = this.session.Broadcaster.Epoch,
            NetId = netId,
            EntityKind = EntityKind.Mob,
            Kind = MobModifierEventKind.ActionStarted,
            SkillId = skill.id.ToString(),
            TargetGuid = targetGuid,
            SkillRatio = data,
            ServerTimeMs = tick,
            LevelId = level?.map?.id?.ToString() ?? string.Empty,
            SubLevelIndex = level?.GetSubLevelIndex() ?? -1,
        });
    }

    public void DispatchSkillExecute(Mob mob, OldSkill skill, long tick) => this.inner.DispatchSkillExecute(mob, skill, tick);

    public void DispatchMobDied(Mob mob, long tick)
        => this.inner.DispatchMobDied(mob, tick);

    public bool DispatchAttackResult(
        Mob mob, AttackData atk, Entity target, long tick)
        => this.inner.DispatchAttackResult(mob, atk, target, tick);

    public bool? DispatchAiLocked(Mob mob, long tick)
        => this.inner.DispatchAiLocked(mob, tick);

    public bool DispatchClientUpdate(Mob mob, long tick)
        => this.inner.DispatchClientUpdate(mob, tick);

    public void Tick(long tick) => this.inner.Tick(tick);

    public void Clear() => this.inner.Clear();
}
