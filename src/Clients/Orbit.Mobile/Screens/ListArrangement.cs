namespace Orbit.Mobile.Screens;

/// <summary>
/// How a reader has asked one of the list screens to be arranged: what order it is in, and what it is
/// narrowed to. The two settings the design hangs under a list screen's own name, beside folders.
///
/// One type for the two screens that offer it rather than one each, because the choices are the same
/// on both and a reader who learns the menu on Notes has learnt it on Tasks - which is the whole point
/// of every list screen being drawn the same way.
/// </summary>
public enum ListSection
{
    Notes,
    Tasks,

    /// <summary>Somewhere on the map worth keeping - see Screens.Places.</summary>
    Places
}

/// <summary>
/// What a list is ordered by. <b>Whatever is chosen, what is pinned comes first</b> - a pin is the
/// reader saying "this one, above the rest", and an order that moved it back down would be answering a
/// question they did not ask. The order below decides everything under the pins.
/// </summary>
public enum ListSortOrder
{
    /// <summary>What changed last, first. What a list of work in progress is usually being read for.</summary>
    Recent,

    /// <summary>By name, which is how somebody looks for one they already know the name of.</summary>
    Name,

    /// <summary>Most important first - see <see cref="Tasks.PriorityChoice"/>.</summary>
    Priority
}

/// <summary>
/// What a list is narrowed to. The same five the dashboard's cards offer, in the same words: a reader
/// who has narrowed a card on the dashboard has already met these.
/// </summary>
public enum ListFilter
{
    All,
    Pinned,
    HighPriority,
    NormalPriority,
    LowPriority
}

/// <summary>Both answers together, which is what is stored and what a screen asks for.</summary>
public sealed record ListArrangement(ListSortOrder SortOrder, ListFilter Filter)
{
    /// <summary>What a reader who has never opened the menu gets.</summary>
    public static readonly ListArrangement Default = new(ListSortOrder.Recent, ListFilter.All);
}

/// <summary>
/// Where those two answers are kept. On the device rather than on the server, as the dashboard's pins
/// and the calendar's reading order are, and for the same reason: it is how one person reads one
/// screen on one phone, and says nothing about the notes or the lists themselves.
/// </summary>
public interface IListArrangementStore
{
    ListArrangement Read(ListSection section);

    void Write(ListSection section, ListArrangement arrangement);
}
