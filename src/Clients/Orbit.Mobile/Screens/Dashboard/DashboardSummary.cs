namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// The counts along the top of the dashboard - what is actually happening today, rather than how much
/// there is in total. Mirrors Orbit.Web's "today strip".
/// </summary>
public sealed record TodaySummary(string Date, int TasksDueToday, int EventsToday, int PendingChatRequests)
{
    public static readonly TodaySummary Nothing = new(string.Empty, 0, 0, 0);

    /// <summary>
    /// Whether anybody is waiting to be answered. A standing "0 new chat requests" is not news, so the
    /// line is left out rather than shown at nought - which is what Orbit.Web's today strip does.
    /// </summary>
    public bool HasChatRequests => PendingChatRequests > 0;
}

/// <summary>
/// One tappable line on the dashboard. Deliberately flat and already formatted: the dashboard shows
/// five different kinds of thing side by side, and giving each its own row type would mean five nearly
/// identical templates in the page for no gain.
/// </summary>
/// <param name="LocalId">What the destination screen is opened with - see <see cref="DashboardCardKind"/>.</param>
/// <param name="Detail">
/// The right-hand side of the row: how long ago a note changed, how far through a task list is, when an
/// event starts. Empty when there is nothing worth saying.
/// </param>
public sealed record DashboardRow(Guid LocalId, string Title, string Detail)
{
    /// <summary>
    /// Whether a hairline is drawn under this row. Set where the card is assembled rather than where the
    /// rows are described, because it is about a row's neighbours and not about the row - and it is true
    /// for all but the last, which is how Orbit.Web's .list-row:last-child rules its own list.
    /// </summary>
    public bool HasDividerUnder { get; init; }

    /// <summary>
    /// How much the list matters, when that is worth saying - Orbit.Web badges the same rows. Empty
    /// for a Normal one, which is what everything is unless somebody said otherwise.
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    public bool HasPriority => Priority.Length > 0;

    /// <summary>
    /// Whether this row carries the coloured dot Orbit.Web draws beside an event. True only on the
    /// calendar card: the other cards' rows have no dot there either.
    /// </summary>
    public bool HasColourDot { get; init; }

    /// <summary>
    /// The event's own colour, as it was chosen - see EventColourChoice. Null for an event that was
    /// never given one, where the app's accent stands in; which colour that is depends on the theme,
    /// so it is settled where the dot is painted rather than here.
    /// </summary>
    public string? Colour { get; init; }

    /// <summary>
    /// Whether this row carries the bar Orbit.Web fills beside the count. False on a list with nothing
    /// in it - an empty list is not nought per cent done, it has nothing to do, and a bar sitting at
    /// zero would read as work nobody has started - and false on every other card's rows.
    /// </summary>
    public bool HasProgress { get; init; }

    /// <summary>How much of the list is done, from 0 to 1. Meaningless unless <see cref="HasProgress"/>.</summary>
    public double Progress { get; init; }

    /// <summary>
    /// Something unread is about this particular row - Orbit.Web's .list-row.row-unseen. Said on the row
    /// as well as on the card because a card saying "something happened here" over six rows leaves the
    /// reader to open all six. Only where a notification can name the thing at all: nothing ever points
    /// at one note or one contact, so those rows are never marked.
    /// </summary>
    public bool HasNews { get; init; }

    /// <summary>
    /// Whether the row leads with the circle a person or a group is drawn as - Orbit.Web's .avatar-sm
    /// in the same place. True only where <see cref="LocalId"/> is somebody: a note's id is not a
    /// person, and a circle made of it would be a colour that means nothing.
    /// </summary>
    public bool HasAvatar { get; init; }

    /// <summary>
    /// Where somebody is, by the server's PresenceStatus name. Empty for a group and for whoever
    /// shared a position: a group is not somewhere anybody is or is not, and a position is a pin
    /// rather than a person to be found.
    /// </summary>
    public string Presence { get; init; } = string.Empty;
}

/// <summary>
/// The cards, in the order Orbit.Web lays them out. Chats appear twice on purpose, as they do there:
/// "Recent chats" answers "who was I just talking to", and "Contacts" is a directory - the same person
/// in both, sorted for two different questions.
/// </summary>
public enum DashboardCardKind
{
    Notes,
    Tasks,
    Upcoming,

    /// <summary>
    /// The shelves themselves, beside the lists they feed - Orbit.Web's own card, in the same place in
    /// the order. Everything else the dashboard draws was reachable from it and this was not, so the
    /// one part of Orbit that answers "have we run out" could only be found through the navigation bar.
    /// </summary>
    Inventories,
    Groups,
    RecentChats,
    Contacts,

    /// <summary>Who is sharing where they are - see SharedLocations, and Orbit.Web's card of the same name.</summary>
    SharedLocations
}

/// <summary>
/// One card: a heading, a count, and the few most relevant rows. Not the whole section - the dashboard
/// is a way in, and the section itself is one tap away on the navigation bar.
/// </summary>
public sealed record DashboardCard(
    DashboardCardKind Kind, string Title, string Count, IReadOnlyList<DashboardRow> Rows, bool IsPinned = false)
{
    /// <summary>
    /// Whether this card offers a filter menu. Derived from whether there is anything to offer, so the
    /// two never disagree - see DashboardViewModel.FilterChoicesFor.
    /// </summary>
    public bool CanBeFiltered { get; init; }

    /// <summary>
    /// Something unread is about something on this card - Orbit.Web's HasUnseenAction on the same card.
    /// Where the rows can carry the mark themselves this is simply whether any of them does; where they
    /// cannot - a shelf about to go off names no shelf - the card is the only place it can be said.
    /// </summary>
    public bool HasUnseenAction { get; init; }

    /// <summary>
    /// The card is on the page because it holds something, and its filter has left none of it. Said in
    /// place of the rows, as Orbit.Web says it - the menu that narrowed the card is in its own header,
    /// so this is a state a reader can get out of.
    /// </summary>
    public bool HasNothingMatching => Rows.Count == 0;
}

/// <summary>
/// Which dashboard cards this reader keeps at the top. Held on the device rather than on the server, as
/// Orbit.Web holds it in localStorage and for the same reason: it is the layout of one page on one
/// device, and describes nothing about the notes, lists or people the cards show.
/// </summary>
public interface IDashboardPinStore
{
    IReadOnlySet<DashboardCardKind> Read();

    void Write(IReadOnlySet<DashboardCardKind> pinned);
}

/// <summary>What one card is showing of what it could show - the same set Orbit.Web offers.</summary>
public enum DashboardCardFilter
{
    All,

    /// <summary>Only what the reader has pinned. Offered where a card's items can be pinned at all.</summary>
    Pinned,
    HighPriority,
    NormalPriority,
    LowPriority
}

/// <summary>One choice in a card's filter menu, with the chosen one marked.</summary>
public sealed record DashboardFilterChoice(DashboardCardKind Kind, DashboardCardFilter Filter, string Name, bool IsChosen);

/// <summary>
/// One line of the "Show on the dashboard" menu: a part of the dashboard and whether it is being shown.
/// Every kind is listed, including the ones with nothing in them today - a card put away has to stay
/// reachable, or there would be no way to bring it back.
/// </summary>
public sealed record DashboardCardChoice(DashboardCardKind Kind, string Name, bool IsShown);

/// <summary>
/// Which parts of the dashboard this reader has put away. Held on the device beside the pins, and for
/// the same reason: it is the layout of one page on one device.
///
/// What is stored is the hidden ones rather than the shown ones, exactly as Orbit.Web stores it - a
/// card added to the dashboard in a later release then appears by default, instead of being invisible
/// to everybody who ever saved a layout.
/// </summary>
public interface IDashboardCardPreferenceStore
{
    IReadOnlySet<DashboardCardKind> ReadHidden();

    void WriteHidden(IReadOnlySet<DashboardCardKind> hidden);

    /// <summary>What each card is filtered down to. A card missing from this is showing everything.</summary>
    IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> ReadFilters();

    void WriteFilters(IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> filters);
}
