using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// Where the app opens: everything on the reader's plate, and a way into each of it. The mobile
/// counterpart of Orbit.Web's Dashboard, and the same landing screen, so the two agree about what
/// "home" means.
///
/// Reads only the local store. Every one of these sections is already kept current by its own
/// synchroniser, so the dashboard has nothing of its own to fetch - which is also why it opens instantly
/// and works with no connection at all.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    /// <summary>
    /// How many rows a card shows. Enough to recognise what is there, few enough that five cards still
    /// fit on a phone - the section itself is one tap away on the navigation bar.
    /// </summary>
    private const int RowsPerCard = 4;

    private readonly LocalNoteRepository _notes;
    private readonly LocalTaskListRepository _taskLists;
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly ChatRepository _chat;
    private readonly TimeProvider _timeProvider;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private TodaySummary _today = TodaySummary.Nothing;

    [ObservableProperty]
    private bool _hasNothing;

    public DashboardViewModel(
        LocalNoteRepository notes, LocalTaskListRepository taskLists,
        LocalCalendarEventRepository calendarEvents, ChatRepository chat, TimeProvider timeProvider,
        IScreenNavigator navigator)
    {
        _notes = notes;
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _chat = chat;
        _timeProvider = timeProvider;
        _navigator = navigator;
    }

    public ObservableCollection<DashboardCard> Cards { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var notes = await _notes.GetAllAsync(cancellationToken);
        var taskLists = await _taskLists.GetAllAsync(cancellationToken);
        var events = await _calendarEvents.GetAllAsync(cancellationToken);
        var contacts = await _chat.GetContactsAsync(cancellationToken);
        var groups = await _chat.GetGroupsAsync(cancellationToken);

        Today = SummariseToday(taskLists, events, contacts);

        Cards.Clear();
        // An empty card is worse than no card: it takes up a phone's screen to say nothing. Each is
        // added only when it has something in it, which is also how the web's dashboard behaves.
        AddCardIfAnything(DashboardCardKind.Notes, "Notes", notes.Count, DescribeNotes(notes));
        AddCardIfAnything(DashboardCardKind.Tasks, "Tasks", taskLists.Count, DescribeTaskLists(taskLists));
        AddCardIfAnything(DashboardCardKind.Events, "Events", events.Count, DescribeEvents(events));
        AddCardIfAnything(DashboardCardKind.Contacts, "Contacts", contacts.Count, DescribeContacts(contacts));
        AddCardIfAnything(DashboardCardKind.Groups, "Groups", groups.Count, DescribeGroups(groups));

        HasNothing = Cards.Count == 0;
    }

    /// <summary>Opens whatever a row stands for, which depends on the card it came from.</summary>
    [RelayCommand]
    private async Task OpenAsync(DashboardRow? row)
    {
        if (row is null || FindCardFor(row) is not { } card)
        {
            return;
        }

        switch (card.Kind)
        {
            case DashboardCardKind.Notes:
                _navigator.ShowNotes();
                break;

            case DashboardCardKind.Tasks:
                _navigator.ShowTaskList(row.LocalId);
                break;

            case DashboardCardKind.Events:
                _navigator.ShowCalendar();
                break;

            case DashboardCardKind.Contacts:
                await OpenConversationAsync(row.LocalId);
                break;

            case DashboardCardKind.Groups:
                await OpenGroupAsync(row.LocalId);
                break;
        }
    }

    private async Task OpenConversationAsync(Guid userId)
    {
        if ((await _chat.GetContactsAsync()).FirstOrDefault(contact => contact.UserId == userId) is { } contact)
        {
            _navigator.ShowConversation(contact);
        }
    }

    private async Task OpenGroupAsync(Guid groupId)
    {
        if (await _chat.FindGroupAsync(groupId) is { } group)
        {
            _navigator.ShowGroupConversation(group);
        }
    }

    private DashboardCard? FindCardFor(DashboardRow row)
        => Cards.FirstOrDefault(card => card.Rows.Contains(row));

    private void AddCardIfAnything(DashboardCardKind kind, string title, int total, IReadOnlyList<DashboardRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        Cards.Add(new DashboardCard(kind, title, total.ToString(), rows));
    }

    private TodaySummary SummariseToday(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        IReadOnlyList<LocalContact> contacts)
    {
        var today = _timeProvider.GetUtcNow().Date;

        return new TodaySummary(
            taskLists
                .SelectMany(list => list.Items)
                .Count(item => !item.IsCompleted && item.DueDateUtc?.Date == today),
            events.Count(calendarEvent => calendarEvent.Details.StartUtc.Date == today),
            // Only requests waiting on the reader. One they sent and nobody has answered is not
            // something they can act on, so counting it would be asking them to do nothing.
            contacts.Count(contact => contact.RequiresApprovalFromCurrentUser));
    }

    private IReadOnlyList<DashboardRow> DescribeNotes(IReadOnlyList<LocalNote> notes)
        => notes
            .OrderByDescending(note => note.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(note => new DashboardRow(note.LocalId, TitleOrPlaceholder(note.Title, "Untitled"), Ago(note.UpdatedAtUtc)))
            .ToList();

    private IReadOnlyList<DashboardRow> DescribeTaskLists(IReadOnlyList<LocalTaskList> taskLists)
        => taskLists
            .OrderByDescending(list => list.IsPinned)
            .ThenByDescending(list => list.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(list => new DashboardRow(list.LocalId, TitleOrPlaceholder(list.Title, "Untitled list"), DescribeProgress(list)))
            .ToList();

    /// <summary>
    /// What is still ahead rather than everything there is. A calendar's value on a home screen is the
    /// next thing, and a list led by last month's events would bury it.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeEvents(IReadOnlyList<LocalCalendarEvent> events)
    {
        var now = _timeProvider.GetUtcNow();

        return events
            .Where(calendarEvent => calendarEvent.Details.EndUtc >= now)
            .OrderBy(calendarEvent => calendarEvent.Details.StartUtc)
            .Take(RowsPerCard)
            .Select(calendarEvent => new DashboardRow(
                calendarEvent.LocalId,
                TitleOrPlaceholder(calendarEvent.Details.Title, "Untitled event"),
                DescribeWhen(calendarEvent.Details.StartUtc, calendarEvent.Details.IsAllDay)))
            .ToList();
    }

    private IReadOnlyList<DashboardRow> DescribeContacts(IReadOnlyList<LocalContact> contacts)
        => contacts
            .OrderByDescending(contact => contact.RequiresApprovalFromCurrentUser)
            .ThenByDescending(contact => contact.LastMessageAtUtc)
            .Take(RowsPerCard)
            .Select(contact => new DashboardRow(
                contact.UserId,
                contact.DisplayName,
                contact.RequiresApprovalFromCurrentUser ? "Wants to chat" : Ago(contact.LastMessageAtUtc)))
            .ToList();

    private IReadOnlyList<DashboardRow> DescribeGroups(IReadOnlyList<LocalChatGroup> groups)
        => groups
            .OrderByDescending(group => group.CreatedAtUtc)
            .Take(RowsPerCard)
            .Select(group => new DashboardRow(group.Id, group.Name, string.Empty))
            .ToList();

    private static string DescribeProgress(LocalTaskList list)
    {
        if (list.Items.Count == 0)
        {
            return list.IsCompleted ? "Done" : string.Empty;
        }

        return $"{list.Items.Count(item => item.IsCompleted)}/{list.Items.Count}";
    }

    private string DescribeWhen(DateTimeOffset startUtc, bool isAllDay)
    {
        var start = startUtc.ToLocalTime();
        if (start.Date == _timeProvider.GetLocalNow().Date)
        {
            return isAllDay ? "Today" : $"Today {start:HH:mm}";
        }

        return isAllDay ? start.ToString("d MMM") : start.ToString("d MMM HH:mm");
    }

    /// <summary>
    /// Coarse on purpose. A dashboard row is glanced at, and "3 days ago" answers what somebody wants to
    /// know there better than a date they then have to work out.
    /// </summary>
    private string Ago(DateTimeOffset moment)
    {
        var elapsed = _timeProvider.GetUtcNow() - moment;

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "Just now",
            { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays}d ago",
            _ => moment.ToLocalTime().ToString("d MMM yyyy")
        };
    }

    private static string TitleOrPlaceholder(string title, string placeholder)
        => title.Trim() is { Length: > 0 } trimmed ? trimmed : placeholder;
}
