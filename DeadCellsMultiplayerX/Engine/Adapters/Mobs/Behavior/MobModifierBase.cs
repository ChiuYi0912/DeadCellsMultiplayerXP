namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

using dc.en;

/// <summary>
/// 泛型修改器基类
/// </summary>
internal abstract class MobModifierBase<TMob> : IMobModifier<TMob>
    where TMob : Mob
{
    /// <inheritdoc />
    public Type MobType => typeof(TMob);

    /// <inheritdoc />
    public virtual void InitHook(IMobHookRegistry hooks)
    {
    }

    /// <inheritdoc />
    public void Attach(Mob mob, IMobModifierContext context)
    {
        if (mob is TMob typed)
        {
            this.Attach(typed, context);
        }
    }

    /// <inheritdoc />
    public void Detach(Mob mob)
    {
        if (mob is TMob typed)
        {
            this.Detach(typed);
        }
    }

    public virtual void Attach(TMob mob, IMobModifierContext context)
    {
    }

    public virtual void Detach(TMob mob)
    {
    }
}
