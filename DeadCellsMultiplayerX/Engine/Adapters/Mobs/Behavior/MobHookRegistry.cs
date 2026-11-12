namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

using dc;
using dc.hl.types;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using Serilog;

/// <summary>
/// 单个修改器实例的 hook 集合
/// 注册器负责按 mob 分发到这里
/// </summary>
internal sealed class MobHookRegistry : IClientMobHooks, IServerMobHooks
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobHookRegistry");

    private readonly List<MobHandler> serverTick = [];
    private readonly List<MobHandler> preUpdate = [];
    private readonly List<MobHandler> fixedUpdate = [];
    private readonly List<MobHandler> postUpdate = [];
    private readonly List<MobBehaviourHandler> behaviourAi = [];
    private readonly List<MobBehaviourHandler> fixedUpdateHandled = [];
    private readonly List<MobInitSkillsHandler> initSkills = [];
    private readonly List<MobSkillPrepareHandler> skillPrepare = [];
    private readonly List<MobSkillExecuteHandle> skillExecute = [];
    private readonly List<MobHandler> mobDied = [];
    private readonly List<MobAttackResultHandler> attackResult = [];
    private readonly List<MobBehaviourHandler> aiLocked = [];
    private readonly List<MobBehaviourHandler> clientUpdate = [];
    private readonly List<MobEventHandler> clientEvent = [];
    private readonly List<MobBehaviourHandler> clientEventHandled = [];

    public void OnServerTick(MobHandler handler)
        => Add(this.serverTick, handler);

    public void OnPreUpdate(MobHandler handler)
        => Add(this.preUpdate, handler);

    public void OnFixedUpdate(MobHandler handler)
        => Add(this.fixedUpdate, handler);

    public void OnPostUpdate(MobHandler handler)
        => Add(this.postUpdate, handler);

    public void OnBehaviourAi(MobBehaviourHandler handler)
        => Add(this.behaviourAi, handler);

    public void OnFixedUpdateHandled(MobBehaviourHandler handler)
        => Add(this.fixedUpdateHandled, handler);

    public void OnInitSkills(MobInitSkillsHandler handler)
        => Add(this.initSkills, handler);

    public void OnSkillPrepare(MobSkillPrepareHandler handler)
        => Add(this.skillPrepare, handler);

    public void OnSkillExecute(MobSkillExecuteHandle handler)
        => Add(this.skillExecute, handler);

    public void OnMobDied(MobHandler handler)
        => Add(this.mobDied, handler);

    public void OnAttackResult(MobAttackResultHandler handler)
        => Add(this.attackResult, handler);

    public void OnAiLocked(MobBehaviourHandler handler)
        => Add(this.aiLocked, handler);

    public void OnClientUpdate(MobBehaviourHandler handler)
        => Add(this.clientUpdate, handler);

    public void OnClientEvent(MobEventHandler handler)
        => Add(this.clientEvent, handler);

    public void OnClientEventHandled(MobBehaviourHandler handler)
        => Add(this.clientEventHandled, handler);

    internal void DispatchServerTick(MobHookContext context)
        => Invoke(this.serverTick, context, "OnServerTick");

    internal void DispatchPreUpdate(MobHookContext context)
        => Invoke(this.preUpdate, context, "OnPreUpdate");

    internal void DispatchFixedUpdate(MobHookContext context)
        => Invoke(this.fixedUpdate, context, "OnFixedUpdate");

    internal void DispatchPostUpdate(MobHookContext context)
        => Invoke(this.postUpdate, context, "OnPostUpdate");

    internal bool DispatchBehaviourAi(MobHookContext context)
        => InvokeAny(this.behaviourAi, context, "OnBehaviourAi");

    internal bool DispatchFixedUpdateHandled(MobHookContext context)
        => InvokeAny(this.fixedUpdateHandled, context, "OnFixedUpdateHandled");

    internal void DispatchInitSkills(
        MobHookContext context, ArrayObj oldSkills, ArrayObj skills)
        => Invoke(this.initSkills, context, oldSkills, skills, "OnInitSkills");

    internal void DispatchSkillPrepare(MobHookContext context, OldSkill skill, Entity target, int? data)
        => Invoke(this.skillPrepare, context, skill, target, data, "OnSkillPrepare");

    internal void DispatchSkillExecute(MobHookContext context, OldSkill skill)
        => Invoke(this.skillExecute, context, skill, "OnSkillExecute");

    internal void DispatchMobDied(MobHookContext context)
        => Invoke(this.mobDied, context, "OnMobDied");

    internal bool DispatchAttackResult(
        MobHookContext context, AttackData atk, Entity target)
        => InvokeAny(this.attackResult, context, atk, target, "OnAttackResult");

    internal bool? DispatchAiLocked(MobHookContext context)
    {
        if (this.aiLocked.Count == 0)
        {
            return null;
        }

        return InvokeAny(this.aiLocked, context, "OnAiLocked");
    }

    internal bool DispatchClientUpdate(MobHookContext context)
        => InvokeAny(this.clientUpdate, context, "OnClientUpdate");

    internal void DispatchClientEvent(MobHookContext context, MobModifierEvent evt)
        => Invoke(this.clientEvent, context, evt, "OnClientEvent");

    internal bool DispatchClientEventHandled(MobHookContext context)
        => InvokeAny(this.clientEventHandled, context, "OnClientEventHandled");

    private static void Add<T>(List<T> list, T handler) where T : Delegate
    {
        if (handler != null)
        {
            list.Add(handler);
        }
    }

    private static void Invoke(
        List<MobHandler> handlers, MobHookContext context, string hook)
    {
        foreach (var handler in handlers)
        {
            handler(context);
        }
    }

    private static void Invoke(
        List<MobInitSkillsHandler> handlers,
        MobHookContext context,
        ArrayObj oldSkills,
        ArrayObj skills,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler(context, oldSkills, skills);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }
        }
    }

    private static void Invoke(
        List<MobSkillExecuteHandle> handlers,
        MobHookContext context,
        OldSkill skill,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler(context, skill);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }
        }
    }

    private static void Invoke(
        List<MobSkillPrepareHandler> handlers,
        MobHookContext context,
        OldSkill skill,
        Entity target,
        int? data,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler(context, skill, target, data);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }
        }
    }

    private static void Invoke(
        List<MobEventHandler> handlers,
        MobHookContext context,
        MobModifierEvent evt,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler(context, evt);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }
        }
    }

    private static bool InvokeAny(
        List<MobBehaviourHandler> handlers,
        MobHookContext context,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                if (handler(context))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }

        }

        return false;
    }

    private static bool InvokeAny(
        List<MobAttackResultHandler> handlers,
        MobHookContext context,
        AttackData atk,
        Entity target,
        string hook)
    {
        foreach (var handler in handlers)
        {
            try
            {
                if (handler(context, atk, target))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hook failed: hook={Hook} mob={Mob} netId={NetId}",
                    hook, context.Mob.GetType().Name, context.NetId);
            }
        }

        return false;
    }
}
