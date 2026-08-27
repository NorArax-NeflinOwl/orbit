namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// The counts along the top of the dashboard - what is actually happening today, rather than how much
/// there is in total. Mirrors Orbit.Web's "today strip".
/// </summary>
public sealed record TodaySummary(int TasksDueToday, int EventsToday, int PendingChatRequests)
{
    public static readonly TodaySummary Nothing = new(0, 0, 0);
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
public sealed record DashboardRow(Guid LocalId, string Title, string Detail);

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
    Contacts
}

/// <summary>
/// One card: a heading, a count, and the few most relevant rows. Not the whole section - the dashboard
/// is a way in, and the section itself is one tap away on the navigation bar.
/// </summary>
public sealed record DashboardCard(
    DashboardCardKind Kind, string Title, string Count, IReadOnlyList<DashboardRow> Rows, bool IsPinned = false);

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
