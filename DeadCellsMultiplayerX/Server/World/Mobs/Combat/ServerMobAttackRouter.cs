namespace DeadCellsMultiplayerX.Server.World.Mobs.Combat;

using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior;

/// <summary>
/// 技能事件路由
/// </summary>
internal sealed class ServerMobAttackRouter(
    IServerMobEventOutbox? events = null) : IServerMobAttackRouter
{
    private readonly IServerMobEventOutbox? events = events;
    private readonly Queue<MobModifierEvent> pendingSkills = new();
    private long nextActionId;

    /// <inheritdoc />
    public void SubmitSkill(MobModifierEvent evt)
    {
        if (evt == null || evt.NetId == 0)
        {
            return;
        }

        this.pendingSkills.Enqueue(evt);
    }

    /// <inheritdoc />
    public IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs)
    {
        if (this.pendingSkills.Count == 0)
        {
            return [];
        }

        var result = new List<MobModifierEvent>();

        while (this.pendingSkills.TryDequeue(out var evt))
        {
            evt.ActionId = ++this.nextActionId;
            if (evt.ServerTimeMs <= 0)
            {
                evt.ServerTimeMs = serverTimeMs;
            }

            result.Add(evt);
            this.events?.Enqueue(evt);
        }

        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        this.pendingSkills.Clear();
        this.nextActionId = 0;
    }
}
