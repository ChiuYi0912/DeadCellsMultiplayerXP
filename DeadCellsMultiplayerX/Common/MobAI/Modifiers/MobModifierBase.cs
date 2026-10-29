using dc.en;

namespace DeadCellsMultiplayerX.Common.MobAI.Modifiers
{
    /// <summary>
    /// 泛型修改器基类
    /// </summary>
    public abstract class MobModifierBase<TMob> : IMobModifier<TMob>
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
                Attach(typed, context);
        }

        /// <inheritdoc />
        public void Detach(Mob mob)
        {
            if (mob is TMob typed)
                Detach(typed);
        }

        public virtual void Attach(TMob mob, IMobModifierContext context)
        {
        }

        public virtual void Detach(TMob mob)
        {
        }
    }
}
