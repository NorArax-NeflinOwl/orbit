namespace Orbit.Web.Services;

/// <summary>
/// Which cards on a list page are chosen to be acted on together, and whether the page is choosing at
/// all - the state behind "select several notes, lists, events or shelves and then file them into a
/// folder or put them away", asked for by the user.
///
/// Owned by the page rather than injected: a selection is about one screenful and dies with it, and two
/// pages sharing one would have the notes somebody picked still picked on the calendar.
///
/// <b>Choosing is a mode.</b> A page not in it behaves exactly as it always did - a press on a card
/// opens it - because the everyday act is opening one thing, and a page where every press might mean
/// "choose" instead is a page nobody can read quickly. The mode is left the moment nothing is chosen
/// by hand (see <see cref="Stop"/>), never on its own: a bar that vanished as the last card was
/// unpicked would take the reader's way back out with it.
/// </summary>
public sealed class PickedThings
{
    private readonly HashSet<Guid> _picked = [];

    /// <summary>Whether the page is choosing - which is what puts a mark on every card and the bar over them.</summary>
    public bool IsPicking { get; private set; }

    public int Count => _picked.Count;

    /// <summary>Whether anything is chosen, which is what decides whether the actions can be pressed.</summary>
    public bool HasAny => _picked.Count > 0;

    /// <summary>The chosen ids, for a caller about to act on each - see the pages, which loop their own per-item call.</summary>
    public IReadOnlyCollection<Guid> Ids => _picked;

    public bool Holds(Guid id) => _picked.Contains(id);

    /// <summary>Starts choosing, with nothing chosen yet.</summary>
    public void Start()
    {
        IsPicking = true;
        _picked.Clear();
    }

    /// <summary>Stops choosing and forgets what was chosen - the one way out of the mode.</summary>
    public void Stop()
    {
        IsPicking = false;
        _picked.Clear();
    }

    /// <summary>
    /// Chooses this one, or unchooses it. Does nothing while the page is not choosing, so a stray press
    /// cannot build a selection nothing is showing.
    /// </summary>
    public void Toggle(Guid id)
    {
        if (!IsPicking)
        {
            return;
        }

        if (!_picked.Add(id))
        {
            _picked.Remove(id);
        }
    }

    /// <summary>
    /// Chooses every one of <paramref name="ids"/>, or unchooses them all when they are already chosen -
    /// the "all of them" press, which is the same button either way and says which it will do by what is
    /// chosen now.
    /// </summary>
    public void ToggleAll(IEnumerable<Guid> ids)
    {
        if (!IsPicking)
        {
            return;
        }

        var all = ids.ToList();
        if (all.Count > 0 && all.All(_picked.Contains))
        {
            _picked.ExceptWith(all);
            return;
        }

        _picked.UnionWith(all);
    }

    /// <summary>
    /// Forgets anything chosen that is no longer on the page - what a folder tab changing, or a card
    /// being put away, leaves behind. Without it the bar went on counting cards nobody could see and
    /// the next press acted on them.
    /// </summary>
    public void KeepOnly(IEnumerable<Guid> shown)
    {
        var onThePage = shown.ToHashSet();
        _picked.IntersectWith(onThePage);
    }
}
