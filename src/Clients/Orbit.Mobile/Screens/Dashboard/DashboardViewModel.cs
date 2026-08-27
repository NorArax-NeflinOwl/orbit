using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Core.Permissions;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// Where the app opens: everything on the reader's plate, and a way into each of it. The mobile
/// counterpart of Orbit.Web's Dashboard, and the same landing screen, so the two agree about what
/// "home" means.
///
/// Shows what is already on the phone first, then synchronises every feature and shows it again if
/// anything changed. Both halves matter: reading the local store first is what makes it open instantly
/// and work with no connection, and synchronising is what stops it from being the one screen nobody
/// refreshes. It used to do only the first half, on the assumption that each section keeps itself
/// current - but a section only does that once its own screen has been opened, so after a sign-in the
/// landing screen stayed empty until the reader had visited Notes, then Tasks, then the calendar.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    /// <summary>How many rows a card shows. Six, as Orbit.Web shows, so the two agree about what fits.</summary>
    private const int RowsPerCard = 6;

    private readonly LocalNoteRepository _notes;
    private readonly LocalTaskListRepository _taskLists;
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly ChatRepository _chat;
    private readonly TimeProvider _timeProvider;
    private readonly Translations _translations;
    private readonly PrivateItemGate _privateItems;
    private readonly EverythingSynchronizer _synchronizer;
    private readonly SyncState _syncState;
    private readonly UserPermissions _permissions;
    private readonly IDashboardPinStore _pins;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private TodaySummary _today = TodaySummary.Nothing;

    [ObservableProperty]
    private bool _hasNothing;

    public DashboardViewModel(
        LocalNoteRepository notes, LocalTaskListRepository taskLists,
        LocalCalendarEventRepository calendarEvents, ChatRepository chat, TimeProvider timeProvider,
        Translations translations, PrivateItemGate privateItems, EverythingSynchronizer synchronizer,
        SyncState syncState, UserPermissions permissions, IDashboardPinStore pins,
        IScreenNavigator navigator)
    {
        _notes = notes;
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _chat = chat;
        _timeProvider = timeProvider;
        _translations = translations;
        _privateItems = privateItems;
        _synchronizer = synchronizer;
        _syncState = syncState;
        _permissions = permissions;
        _pins = pins;
        _navigator = navigator;
    }

    public ObservableCollection<DashboardCard> Cards { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Before the summary, not after: a card whose every row leads to "not unlocked" would otherwise
        // be drawn first and taken away a moment later.
        await _permissions.EnsureLoadedAsync(cancellationToken);
        await ShowStoredSummaryAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        _syncState.RecordStarted();
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            if (result.ReachedTheServer)
            {
                _syncState.RecordSucceeded();
            }
            else
            {
                _syncState.RecordFailed();
            }

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredSummaryAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync. The command is started without being awaited, so this
            // must not escape.
        }
    }

    private async Task ShowStoredSummaryAsync(CancellationToken cancellationToken)
    {
        var notes = await _notes.GetAllAsync(cancellationToken);
        var taskLists = await _taskLists.GetAllAsync(cancellationToken);
        var events = await _calendarEvents.GetAllAsync(cancellationToken);
        // Nothing conversational is shown to an account that cannot hold a conversation, as the web's
        // dashboard does it - a card whose every row leads to "not unlocked" is worse than no card.
        var contacts = _permissions.Has(ApplicationPermission.Chat)
            ? await _chat.GetContactsAsync(cancellationToken)
            : [];
        var groups = _permissions.Has(ApplicationPermission.GroupChat)
            ? await _chat.GetGroupsAsync(cancellationToken)
            : [];

        Today = SummariseToday(taskLists, events, contacts);

        _built.Clear();
        // An empty card is worse than no card: it takes up a phone's screen to say nothing. Each is
        // added only when it has something in it, which is also how the web's dashboard behaves.
        AddCardIfAnything(
            DashboardCardKind.Notes, _translations["Notes"], DescribeNotes(notes), notes.Count(CanBeShown));
        AddCardIfAnything(
            DashboardCardKind.Tasks, _translations["Tasks"], DescribeTaskLists(taskLists), taskLists.Count(CanBeShown));
        AddCardIfAnything(DashboardCardKind.Upcoming, _translations["Upcoming"], DescribeEvents(events), events.Count);
        AddCardIfAnything(DashboardCardKind.Groups, _translations["Groups"], DescribeGroups(groups), groups.Count);
        AddCardIfAnything(DashboardCardKind.RecentChats, _translations["Recent chats"], DescribeRecentChats(contacts), contacts.Count);
        AddCardIfAnything(DashboardCardKind.Contacts, _translations["Contacts"], DescribeDirectory(contacts), DirectoryOf(contacts).Count);

        ShowCards();
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

            case DashboardCardKind.Upcoming:
                _navigator.ShowCalendar();
                break;

            case DashboardCardKind.RecentChats:
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

    /// <summary>
    /// Cards are built in the order Orbit.Web lays them out, then the pinned ones are lifted to the top
    /// - so pinning changes where a card sits without changing the order of everything else.
    /// </summary>
    private void AddCardIfAnything(DashboardCardKind kind, string title, IReadOnlyList<DashboardRow> rows, int total)
    {
        if (rows.Count == 0)
        {
            return;
        }

        _built.Add(new DashboardCard(kind, title, total.ToString(), rows, _pins.Read().Contains(kind)));
    }

    /// <summary>The cards as built, before pinning moves any of them.</summary>
    private readonly List<DashboardCard> _built = [];

    private void ShowCards()
    {
        Cards.Clear();
        foreach (var card in _built.OrderByDescending(card => card.IsPinned))
        {
            Cards.Add(card);
        }

        HasNothing = Cards.Count == 0;
    }

    /// <summary>Keeps a card at the top of this page on this device, or lets it back down.</summary>
    [RelayCommand]
    private void TogglePin(DashboardCard? card)
    {
        if (card is null)
        {
            return;
        }

        var pinned = _pins.Read().ToHashSet();
        if (!pinned.Add(card.Kind))
        {
            pinned.Remove(card.Kind);
        }

        _pins.Write(pinned);

        for (var index = 0; index < _built.Count; index++)
        {
            if (_built[index].Kind == card.Kind)
            {
                _built[index] = _built[index] with { IsPinned = pinned.Contains(card.Kind) };
            }
        }

        ShowCards();
    }

    /// <summary>Whether something private may be named here at all - see PrivateItemGate.</summary>
    private bool CanBeShown(LocalNote note) => !note.IsPrivate || _privateItems.IsUnlocked;

    private bool CanBeShown(LocalTaskList list) => !list.IsPrivate || _privateItems.IsUnlocked;

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

    /// <summary>
    /// A private note's title is the thing the gate hides, and the dashboard shows titles - so leaving
    /// it out here would have hidden a note on its own screen and named it on the landing one. Found by
    /// walking the app: the gate was locked and the title was on the dashboard.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeNotes(IReadOnlyList<LocalNote> notes)
        => notes
            .Where(CanBeShown)
            .OrderByDescending(note => note.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(note => new DashboardRow(note.LocalId, TitleOrPlaceholder(note.Title, _translations["Untitled"]), Ago(note.UpdatedAtUtc)))
            .ToList();

    private IReadOnlyList<DashboardRow> DescribeTaskLists(IReadOnlyList<LocalTaskList> taskLists)
        => taskLists
            .Where(CanBeShown)
            .OrderByDescending(list => list.IsPinned)
            .ThenByDescending(list => list.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(list => new DashboardRow(list.LocalId, TitleOrPlaceholder(list.Title, _translations["Untitled list"]), DescribeProgress(list)))
            .ToList();

    /// <summary>
    /// Everything on the calendar, soonest first - not only what is ahead. Filtering to the future reads
    /// as the better idea and is a divergence: Orbit.Web shows the lot, and an account whose events have
    /// all been and gone would show a calendar card there and none here.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeEvents(IReadOnlyList<LocalCalendarEvent> events)
        => events
            .OrderBy(calendarEvent => calendarEvent.Details.StartUtc)
            .Take(RowsPerCard)
            .Select(calendarEvent => new DashboardRow(
                calendarEvent.LocalId,
                TitleOrPlaceholder(calendarEvent.Details.Title, _translations["Untitled event"]),
                DescribeWhen(calendarEvent.Details.StartUtc, calendarEvent.Details.IsAllDay)))
            .ToList();

    /// <summary>Who was last talking, most recent first, with anybody waiting on an answer at the top.</summary>
    private IReadOnlyList<DashboardRow> DescribeRecentChats(IReadOnlyList<LocalContact> contacts)
        => contacts
            .OrderByDescending(contact => contact.RequiresApprovalFromCurrentUser)
            .ThenByDescending(contact => contact.LastMessageAtUtc)
            .Take(RowsPerCard)
            .Select(contact => new DashboardRow(
                contact.UserId,
                contact.DisplayName,
                contact.RequiresApprovalFromCurrentUser ? _translations["Wants to chat"] : Ago(contact.LastMessageAtUtc)))
            .ToList();

    /// <summary>
    /// A plain directory, alphabetical. Leaves out conversations nobody has answered yet, so an
    /// unanswered request shows up once - in Recent chats - rather than in both.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeDirectory(IReadOnlyList<LocalContact> contacts)
        => DirectoryOf(contacts)
            .Take(RowsPerCard)
            .Select(contact => new DashboardRow(contact.UserId, contact.DisplayName, string.Empty))
            .ToList();

    private static IReadOnlyList<LocalContact> DirectoryOf(IReadOnlyList<LocalContact> contacts)
        => contacts
            .Where(contact => !contact.RequiresApprovalFromCurrentUser && !contact.IsPendingApprovalFromOtherParty)
            .OrderBy(contact => contact.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private IReadOnlyList<DashboardRow> DescribeGroups(IReadOnlyList<LocalChatGroup> groups)
        => groups
            .OrderByDescending(group => group.CreatedAtUtc)
            .Take(RowsPerCard)
            .Select(group => new DashboardRow(group.Id, group.Name, string.Empty))
            .ToList();

    private string DescribeProgress(LocalTaskList list)
    {
        if (list.Items.Count == 0)
        {
            return list.IsCompleted ? _translations["Done"] : string.Empty;
        }

        return $"{list.Items.Count(item => item.IsCompleted)}/{list.Items.Count}";
    }

    private string DescribeWhen(DateTimeOffset startUtc, bool isAllDay)
    {
        var start = startUtc.ToLocalTime();
        var today = _timeProvider.GetLocalNow().Date;
        var day = start.Date == today ? _translations["Today"]
            : start.Date == today.AddDays(1) ? _translations["Tomorrow"]
            : start.ToString("ddd d", _translations.DisplayCulture);

        return isAllDay ? day : $"{day} {start:HH:mm}";
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
            { TotalMinutes: < 1 } => _translations["Just now"],
            { TotalHours: < 1 } => _translations.Format("{0}m ago", (int)elapsed.TotalMinutes),
            { TotalDays: < 1 } => _translations.Format("{0}h ago", (int)elapsed.TotalHours),
            { TotalDays: < 30 } => _translations.Format("{0}d ago", (int)elapsed.TotalDays),
            _ => moment.ToLocalTime().ToString("d MMM yyyy", _translations.DisplayCulture)
        };
    }

    private string TitleOrPlaceholder(string title, string placeholder)
        => title.Trim() is { Length: > 0 } trimmed ? trimmed : placeholder;
}
