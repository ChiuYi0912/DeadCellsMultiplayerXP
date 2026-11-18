namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc.cine;
using HaxeProxy.Runtime;

/// <summary>Session-owned, one-shot subscription to native level entry completion.</summary>
internal sealed class GuestLevelEntrySubscription : IDisposable
{
    private LevelTransition? transition;
    private HlAction? installed;
    private HlAction? previous;
    private Action? entered;

    public GuestLevelEntrySubscription(LevelTransition transition, Action entered)
    {
        this.transition = transition;
        this.entered = entered;
        var previous = transition.onEnteredLevel;
        this.previous = previous;
        this.installed = new HlAction(() =>
        {
            var notify = this.entered;
            this.entered = null;
            // A retained callback still forwards native behavior after session cleanup.
            previous?.Invoke();
            notify?.Invoke();
        });
        transition.onEnteredLevel += this.installed;
    }

    public void Dispose()
    {
        var transition = this.transition;
        var installed = this.installed;
        var previous = this.previous;
        this.transition = null;
        this.installed = null;
        this.previous = null;
        this.entered = null;
        if (transition != null && installed != null && transition.onEnteredLevel == installed)
        {
            transition.onEnteredLevel -= previous;
        }
    }
}
