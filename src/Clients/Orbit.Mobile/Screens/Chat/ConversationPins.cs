namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// Where this device keeps the pinned conversations - see <see cref="ConversationPins"/>. An interface
/// so the screens stay testable, the way the dashboard's pins and the checklist's reading are kept.
/// </summary>
public interface IConversationPinStore
{
    IReadOnlySet<Guid> Read();

    void Write(IReadOnlySet<Guid> pinned);
}

/// <summary>
/// The people and groups this reader keeps at the top of their lists.
///
/// Kept on the device, as Orbit.Web keeps its own: pinning says which conversations matter to one
/// person reading one screen, and the other party has their own answer. It is also why this needs no
/// column anywhere - a preference about reading is not something the server has to know.
///
/// People and groups share one set. An id means one conversation whichever kind it is, and a reader who
/// pins four things wants those four at the top rather than two lists' worth of rules.
/// </summary>
public sealed class ConversationPins
{
    private readonly IConversationPinStore _store;
    private readonly HashSet<Guid> _pinned;

    public ConversationPins(IConversationPinStore store)
    {
        _store = store;
        _pinned = [.. store.Read()];
    }

    public bool IsPinned(Guid id) => _pinned.Contains(id);

    /// <summary>Pins it, or lets it go back into the order it was in.</summary>
    public void Toggle(Guid id)
    {
        if (!_pinned.Add(id))
        {
            _pinned.Remove(id);
        }

        _store.Write(_pinned);
    }

    /// <summary>
    /// The same rows, pinned ones first and everything else after, each half keeping the order it came
    /// in. A pinned row is still one of the list - lifted to the top rather than taken out of it, so
    /// the reader can still find it where the sort would otherwise have put it once it is unpinned.
    /// </summary>
    public IEnumerable<TRow> PinnedFirst<TRow>(IEnumerable<TRow> rows, Func<TRow, Guid> idOf)
    {
        var inOrder = rows.ToList();
        return inOrder.Where(row => IsPinned(idOf(row))).Concat(inOrder.Where(row => !IsPinned(idOf(row))));
    }
}
