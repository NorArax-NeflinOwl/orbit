namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// The counts along the top of the dashboard - what is actually happening today, rather than how much
/// there is in total. Mirrors Orbit.Web's "today strip".
/// </summary>
public sealed record TodaySummary(string Date, int TasksDueToday, int EventsToday, int PendingChatRequests)
{
    public static readonly TodaySummary Nothing = new(string.Empty, 0, 0, 0);
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
    /// Whether a hairline is drawn above this row. Set where the card is assembled rather than where
    /// the rows are described, because it is about a row's neighbours and not about the row - and it is
    /// true for all but the first, which is how Orbit.Web's .list-row rules its own list.
    /// </summary>
    public bool ShowsSeparator { get; init; }

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

/// <summary>

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
