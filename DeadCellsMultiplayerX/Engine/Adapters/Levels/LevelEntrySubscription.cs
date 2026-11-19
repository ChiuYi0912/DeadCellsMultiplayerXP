namespace DeadCellsMultiplayerX.Engine.Adapters.Levels;

using dc.cine;
using HaxeProxy.Runtime;

/// <summary>One-shot entry subscription, owned by the native session or loader.</summary>
internal sealed class LevelEntrySubscription : IDisposable
{
    private LevelTransition? transition;
    private HlAction? installed;
    private HlAction? previous;
    private Action? entered;

    public LevelEntrySubscription(LevelTransition transition, Action entered)
    {
        this.transition = transition;
        this.entered = entered;
        var previous = this.previous = transition.onEnteredLevel;
        this.installed = new HlAction(() =>
        {
            var notify = this.entered;
            this.entered = null;
            previous?.Invoke();
            notify?.Invoke();
        });
        transition.onEnteredLevel = this.installed;
    }

    public void Dispose()
    {
        if (this.transition != null && this.transition.onEnteredLevel == this.installed)
        {
            this.transition.onEnteredLevel = this.previous;
        }

        this.entered = null;
        this.transition = null;
        this.installed = null;
        this.previous = null;
    }
}
