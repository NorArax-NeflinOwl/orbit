using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Widgets;

/// <summary>
/// Which of the dashboard's cards a widget is showing. The reader picks one when they place it - see
/// the configuration activity in Orbit.Maui.
///
/// Five of the dashboard's nine. The four left out are the ones about people - Groups, Recent chats,
/// Contacts, and who is sharing where they are. Two reasons, either of which would be enough: a home
/// screen is on show to whoever can see the phone, and a list of who somebody talks to is the last
/// thing that belongs there; and a message cannot be opened outside the app at all, since it is sealed
/// to a key the widget has no business holding - so those cards would be names with nothing under them.
/// </summary>
public enum WidgetCard
{
    Notes,
    Tasks,
    Upcoming,
    Inventory,
    Places
}

/// <summary>
/// One row of a card on the home screen: what it is, the one thing worth saying about it, and where
/// tapping it leads.
/// </summary>
/// <param name="Detail">
/// The right-hand side - how long ago a note changed, how far through a list is, when something
/// happens. Empty when there is nothing worth saying, as the dashboard's own rows have it.
/// </param>
/// <param name="Url">
/// In the paths a notification uses - see NotificationDestination - and empty for a row with nowhere
/// of its own to go, which opens Orbit where it would have opened anyway. A note has no path: the app
/// has a screen for one, the notification paths do not name it, and inventing a path here would mean
/// two ways into the app from outside it.
/// </param>
public sealed record CardLine(string What, string Detail, string Url);

/// <summary>
/// One of the dashboard's cards, drawn on the home screen. Asked for on 2026-09-20: "any tile from the
/// dashboard", beside the calendar view (<see cref="MonthAtAGlance"/>).
///
/// It is the dashboard's card rather than the dashboard's section: a heading, and the few rows that
/// card would show, in the order it shows them. The same two rules every widget here follows -
/// <b>nothing private is ever named</b> and <b>nothing at all is shown to a phone nobody is signed in
/// on</b> - and for the reasons <see cref="TodayAtAGlance"/> gives at length.
/// </summary>
/// <param name="Message">
/// What it says instead of rows: a card with nothing on it, or a phone nobody is signed in on. Empty
/// when there are rows. A widget showing an empty box reads as broken.
/// </param>
/// <param name="Url">Where the card itself leads, for the press that lands between the rows.</param>
public sealed record CardAtAGlance(
    string Heading, IReadOnlyList<CardLine> Lines, string Message, string Url = "")
{
    /// <summary>How many rows fit - the same four the other widget shows, for the same reason.</summary>
    public const int MostLines = 4;

    /// <summary>How far ahead the Upcoming card looks, which is what the dashboard's own horizon starts at.</summary>
    private static readonly TimeSpan HowFarAhead = TimeSpan.FromDays(14);

    /// <inheritdoc cref="TodayAtAGlance.ForNobodySignedIn"/>
    public static CardAtAGlance ForNobodySignedIn(Translations translations)
        => new(string.Empty, [], translations["Open Orbit to see this"]);

    /// <summary>
    /// A widget placed but never answered for - which a launcher can leave behind, since Android 12
    /// places a widget with a configuration activity straight away and defers the question to a tap on
    /// it. Not the same as nobody being signed in, and saying so would send somebody to open an app
    /// they are already signed into.
    /// </summary>
    public static CardAtAGlance NotChosenYet(Translations translations)
        => new(string.Empty, [], translations["Tap to choose what this shows"]);

    /// <summary>
    /// The chosen card as it stands at <paramref name="now"/>, from what this phone holds. Everything
    /// is worked out in the phone's own time zone, as everywhere else here.
    /// </summary>
    public static CardAtAGlance Of(
        WidgetCard card, WhatThePhoneHolds held, DateTimeOffset now, Translations translations)
    {
        var lines = card switch
        {
            WidgetCard.Notes => Notes(held.Notes, now, translations),
            WidgetCard.Tasks => TaskLists(held.TaskLists, translations),
            WidgetCard.Upcoming => Upcoming(held.Events, now, translations),
            WidgetCard.Inventory => Inventories(held.Inventories, translations),
            _ => Places(held.Places)
        };

        return new CardAtAGlance(
            HeadingOf(card, translations),
            [.. lines.Take(MostLines)],
            lines.Count == 0 ? translations["Nothing on this yet"] : string.Empty,
            WhereTheCardLeads(card));
    }

    /// <summary>The card's own name, which is the one the dashboard gives it.</summary>
    public static string HeadingOf(WidgetCard card, Translations translations) => card switch
    {
        WidgetCard.Notes => translations["Notes"],
        WidgetCard.Tasks => translations["Tasks"],
        WidgetCard.Upcoming => translations["Upcoming"],
        WidgetCard.Inventory => translations["Inventory"],
        _ => translations["Places you keep"]
    };

    /// <summary>
    /// Where a press that lands on the card rather than on a row leads. Notes have no path of their own
    /// at all - see <see cref="CardLine.Url"/> - so that card opens Orbit and nothing more.
    /// </summary>
    private static string WhereTheCardLeads(WidgetCard card) => card switch
    {
        WidgetCard.Upcoming => "/calendar",
        WidgetCard.Inventory => "/inventory",
        WidgetCard.Places => "/map",
        _ => string.Empty
    };

    private static IReadOnlyList<CardLine> Notes(
        IReadOnlyList<LocalNote> notes, DateTimeOffset now, Translations translations)
        => [.. notes
            .Where(CanBeShown)
            .OrderByDescending(note => note.UpdatedAtUtc)
            .Select(note => new CardLine(
                note.Title.Length > 0 ? note.Title : translations["Untitled"],
                LastChanged.Describe(note.UpdatedAtUtc, now, translations),
                string.Empty))];

    private static IReadOnlyList<CardLine> TaskLists(
        IReadOnlyList<LocalTaskList> taskLists, Translations translations)
        => [.. taskLists
            .Where(CanBeShown)
            .OrderByDescending(taskList => taskList.UpdatedAtUtc)
            .Select(taskList => new CardLine(
                taskList.Title.Length > 0 ? translations.Written(taskList.Title) : translations["Untitled list"],
                taskList.Items.Count == 0
                    ? string.Empty
                    : $"{taskList.Items.Count(item => item.IsCompleted)}/{taskList.Items.Count}",
                // The one card whose rows have somewhere to go: a list the server knows can be opened
                // by the path a deadline's notification already uses.
                taskList.ServerId is { } serverId ? $"/tasks/{serverId}" : string.Empty))];

    /// <summary>
    /// What is coming up, soonest first - appointments that have not finished yet, with their repeats
    /// expanded (see CalendarOccurrences). A fortnight ahead: the card has four rows, and something
    /// three months away is not what a home screen is for.
    /// </summary>
    private static IReadOnlyList<CardLine> Upcoming(
        IReadOnlyList<LocalCalendarEvent> events, DateTimeOffset now, Translations translations)
        => [.. CalendarOccurrences.Between(events, now, now + HowFarAhead)
            .Where(occurrence => occurrence.Details.IsAllDay || occurrence.Details.EndUtc > now)
            .OrderBy(occurrence => occurrence.Details.StartUtc)
            .Select(occurrence => new CardLine(
                occurrence.Details.Title, When(occurrence, translations), "/calendar"))];

    /// <summary>
    /// The day, and the time after it unless it is an all-day event. Two formats joined rather than one
    /// pattern: in a custom format string "t" is the first letter of the AM designator, not the short
    /// time - so "d MMM, t" read "24 Sep, A" on the home screen, which is where it was caught.
    /// </summary>
    private static string When(LocalCalendarEvent occurrence, Translations translations)
    {
        var start = occurrence.Details.StartUtc.ToLocalTime();
        var day = start.ToString("d MMM", translations.DisplayCulture);

        return occurrence.Details.IsAllDay
            ? day
            : $"{day}, {start.ToString("t", translations.DisplayCulture)}";
    }

    private static IReadOnlyList<CardLine> Inventories(
        IReadOnlyList<LocalInventory> inventories, Translations translations)
        => [.. inventories
            .Where(CanBeShown)
            .OrderByDescending(inventory => inventory.UpdatedAtUtc)
            .Select(inventory => new CardLine(
                inventory.Name.Length > 0 ? inventory.Name : translations["Untitled"],
                translations.Format("Items: {0}", inventory.Items.Count),
                inventory.ServerId is { } serverId ? $"/inventory/{serverId}" : "/inventory"))];

    /// <summary>
    /// The places kept on the map. Most of these are private - a place is private unless its owner said
    /// otherwise (see LocalPlace) - so this card is often empty, which is the honest answer rather than
    /// a reason to let one through.
    /// </summary>
    private static IReadOnlyList<CardLine> Places(IReadOnlyList<LocalPlace> places)
        => [.. places
            .Where(CanBeShown)
            .OrderByDescending(place => place.UpdatedAtUtc)
            .Select(place => new CardLine(place.Name, place.Address, "/map"))];

    /// <summary>
    /// Whether a thing may be named on a home screen at all: not put away, not private, not sealed. The
    /// first is what the dashboard itself asks; the other two are this file's own rule.
    /// </summary>
    private static bool CanBeShown(LocalNote note)
        => !note.IsArchived && !note.IsPrivate && !note.IsSealed;

    /// <inheritdoc cref="CanBeShown(LocalNote)"/>
    private static bool CanBeShown(LocalTaskList taskList)
        => !taskList.IsArchived && !taskList.IsPrivate && !taskList.IsSealed;

    /// <inheritdoc cref="CanBeShown(LocalNote)"/>
    private static bool CanBeShown(LocalInventory inventory)
        => !inventory.IsArchived && !inventory.IsPrivate && !inventory.IsSealed;

    /// <inheritdoc cref="CanBeShown(LocalNote)"/>
    private static bool CanBeShown(LocalPlace place)
        => !place.IsArchived && !place.IsPrivate && !place.IsSealed;
}

/// <summary>
/// The tables a widget reads, handed over together rather than one argument per card - a card is chosen
/// at the moment it is drawn, and the reader may have picked any of them.
/// </summary>
public sealed record WhatThePhoneHolds(
    IReadOnlyList<LocalNote> Notes,
    IReadOnlyList<LocalTaskList> TaskLists,
    IReadOnlyList<LocalCalendarEvent> Events,
    IReadOnlyList<LocalInventory> Inventories,
    IReadOnlyList<LocalPlace> Places)
{
    public static WhatThePhoneHolds Nothing { get; } = new([], [], [], [], []);
}
