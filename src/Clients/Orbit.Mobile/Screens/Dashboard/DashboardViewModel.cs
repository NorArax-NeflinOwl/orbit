using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Core.Permissions;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Location;
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
    private readonly IDashboardCardPreferenceStore _visibility;
    private readonly SharedLocations _sharedLocations;
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
        IDashboardCardPreferenceStore visibility, SharedLocations sharedLocations,
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
        _visibility = visibility;
        _hidden = [.. visibility.ReadHidden()];
        _filters = visibility.ReadFilters().ToDictionary(filter => filter.Key, filter => filter.Value);
        _sharedLocations = sharedLocations;

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
        var contacts = _permissions.Has(ApplicationPermission.Contacts)
            ? await _chat.GetContactsAsync(cancellationToken)
            : [];
        var groups = _permissions.Has(ApplicationPermission.Chat)
            ? await _chat.GetGroupsAsync(cancellationToken)
            : [];

        // Who is sharing where they are, which Orbit.Web puts on the dashboard too - and which is worth
        // more on a phone, where the reader is the one out and about. Asked only where the feature is
        // unlocked, like the cards above, and never worth a message when it cannot be reached.
        var sharedPositions = _permissions.Has(ApplicationPermission.Location)
            ? await ReadSharedPositionsAsync(cancellationToken)
            : [];

        Today = SummariseToday(taskLists, events, contacts);

        _built.Clear();
        // An empty card is worse than no card: it takes up a phone's screen to say nothing. Each is
        // added only when it has something in it, which is also how the web's dashboard behaves.
        // Filtered before both the rows and the count, so a card that says "3" is showing three - the
        // same as Orbit.Web, whose count is of what it is about to draw rather than of everything.
        var shownNotes = notes.Where(note => Passes(DashboardCardKind.Notes, note.IsPinned)).ToList();
        var shownTaskLists = taskLists.Where(list => Passes(DashboardCardKind.Tasks, list.IsPinned)).ToList();
        var shownEvents = events.Where(PassesPriority).ToList();

        AddCardIfAnything(
            DashboardCardKind.Notes, _translations["Notes"], DescribeNotes(shownNotes), shownNotes.Count(CanBeShown));
        AddCardIfAnything(
            DashboardCardKind.Tasks, _translations["Tasks"], DescribeTaskLists(shownTaskLists), shownTaskLists.Count(CanBeShown));
        AddCardIfAnything(DashboardCardKind.Upcoming, _translations["Upcoming"], DescribeEvents(shownEvents), shownEvents.Count);
        AddCardIfAnything(DashboardCardKind.Groups, _translations["Groups"], DescribeGroups(groups), groups.Count);
        AddCardIfAnything(
            DashboardCardKind.SharedLocations, _translations["Shared with you"],
            DescribeSharedPositions(sharedPositions), sharedPositions.Count);
        AddCardIfAnything(DashboardCardKind.RecentChats, _translations["Recent chats"], DescribeRecentChats(contacts), contacts.Count);
        AddCardIfAnything(DashboardCardKind.Contacts, _translations["Contacts"], DescribeDirectory(contacts), DirectoryOf(contacts).Count);

        ShowCards();
    }

    /// <summary>
    /// Best-effort: the dashboard is a way in, and a card missing because the server could not be
    /// reached is better than a dashboard that refuses to draw.
    /// </summary>
    private async Task<IReadOnlyList<ReceivedPosition>> ReadSharedPositionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _sharedLocations.ReadSharedWithMeAsync(cancellationToken);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException or EncryptionKeyLockedException)
        {
            // A locked chat key is an ordinary state, not a failure: a position is sealed with the same
            // key chat uses, and until the reader unlocks it there is nothing here to read. The map
            // sends them to the gate when they go looking; the dashboard just leaves the card out
            // rather than taking the whole screen down for it.
            return [];
        }
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

            // A position is a pin, and the map is the only place one can be looked at.
            case DashboardCardKind.SharedLocations:
                _navigator.ShowMap();
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

        var ruled = rows.Select((row, position) => row with { ShowsSeparator = position > 0 }).ToList();
        _built.Add(new DashboardCard(kind, title, total.ToString(), ruled, _pins.Read().Contains(kind))
        {
            CanBeFiltered = OptionsFor(kind).Count > 0
        });
    }

    /// <summary>
    /// Whether a pinnable thing survives its card's filter. "Pinned" is the only filter these cards
    /// offer, so anything else lets everything through.
    /// </summary>
    private bool Passes(DashboardCardKind kind, bool isPinned)
        => FilterFor(kind) is not DashboardCardFilter.Pinned || isPinned;

    /// <summary>
    /// Whether an event survives the Upcoming card's filter. Priority travels as a name - see
    /// CalendarEventDetailsDto.Priority - and one this build does not know lets the event through
    /// rather than hiding it, because a hidden event is worse than an unfiltered one.
    /// </summary>
    private bool PassesPriority(LocalCalendarEvent calendarEvent)
        => FilterFor(DashboardCardKind.Upcoming) switch
        {
            DashboardCardFilter.HighPriority => calendarEvent.Details.Priority == "High",
            DashboardCardFilter.NormalPriority => calendarEvent.Details.Priority == "Normal",
            DashboardCardFilter.LowPriority => calendarEvent.Details.Priority == "Low",
            _ => true
        };

    private DashboardCardFilter FilterFor(DashboardCardKind kind)
        => _filters.TryGetValue(kind, out var filter) ? filter : DashboardCardFilter.All;

    /// <summary>
    /// What a card's filter menu offers. Notes and lists can be narrowed to what is pinned; events to
    /// one priority. The other cards hold things with neither, so they get no menu at all - the same
    /// three that go without one on Orbit.Web.
    /// </summary>
    public IReadOnlyList<DashboardFilterChoice> FilterChoicesFor(DashboardCardKind kind)
        => OptionsFor(kind)
            .Select(option => new DashboardFilterChoice(
                kind, option, NameOfFilter(option), option == FilterFor(kind)))
            .ToList();

    private static IReadOnlyList<DashboardCardFilter> OptionsFor(DashboardCardKind kind) => kind switch
    {
        DashboardCardKind.Notes or DashboardCardKind.Tasks =>
            [DashboardCardFilter.All, DashboardCardFilter.Pinned],
        DashboardCardKind.Upcoming =>
            [DashboardCardFilter.All, DashboardCardFilter.HighPriority,
             DashboardCardFilter.NormalPriority, DashboardCardFilter.LowPriority],
        _ => []
    };

    private string NameOfFilter(DashboardCardFilter filter) => filter switch
    {
        DashboardCardFilter.Pinned => _translations["Pinned"],
        DashboardCardFilter.HighPriority => _translations["High"],
        DashboardCardFilter.NormalPriority => _translations["Normal"],
        DashboardCardFilter.LowPriority => _translations["Low"],
        _ => _translations["All"]
    };

    /// <summary>
    /// Narrows a card, or widens it again. Written through at once, like the parts put away above -
    /// and the whole dashboard is rebuilt, because the count on the card has to agree with the rows.
    /// </summary>
    [RelayCommand]
    private async Task ChooseFilterAsync(DashboardFilterChoice? choice, CancellationToken cancellationToken)
    {
        if (choice is null)
        {
            return;
        }

        if (choice.Filter is DashboardCardFilter.All)
        {
            _filters.Remove(choice.Kind);
        }
        else
        {
            _filters[choice.Kind] = choice.Filter;
        }

        _visibility.WriteFilters(_filters);
        // Rebuilt from the store rather than reloaded: narrowing a card is a preference on this device
        // and has no business asking the server anything. It does have to rebuild rather than just
        // refilter, because the count on the card has to agree with the rows under it.
        await ShowStoredSummaryAsync(cancellationToken);
    }

    /// <summary>Which parts this reader has put away - see IDashboardCardPreferenceStore.</summary>
    private readonly HashSet<DashboardCardKind> _hidden;

    /// <summary>What each card is filtered down to. A card missing from here shows everything.</summary>
    private readonly Dictionary<DashboardCardKind, DashboardCardFilter> _filters;

    /// <summary>
    /// The "Show on the dashboard" menu Orbit.Web puts under the page's own overflow. Every kind is
    /// listed, not only the ones with something in them: a card that is both empty and put away would
    /// otherwise have no way back.
    /// </summary>
    public ObservableCollection<DashboardCardChoice> CardChoices { get; } = [];

    [ObservableProperty]
    private bool _isChoosingCards;

    /// <summary>Opens and closes the menu. It stays open while several are changed, as the web's does.</summary>
    [RelayCommand]
    private void ToggleCardChoices()
    {
        IsChoosingCards = !IsChoosingCards;
        if (IsChoosingCards)
        {
            ShowCardChoices();
        }
    }

    /// <summary>
    /// Puts a part of the dashboard away, or brings it back. Written through at once rather than on
    /// closing the menu: a preference that survives only a tidy exit is one that gets lost.
    /// </summary>
    [RelayCommand]
    private void ToggleCardShown(DashboardCardChoice? choice)
    {
        if (choice is null)
        {
            return;
        }

        if (!_hidden.Remove(choice.Kind))
        {
            _hidden.Add(choice.Kind);
        }

        _visibility.WriteHidden(_hidden);
        ShowCardChoices();
        ShowCards();
    }

    private void ShowCardChoices()
    {
        CardChoices.Clear();
        foreach (var kind in Enum.GetValues<DashboardCardKind>())
        {
            CardChoices.Add(new DashboardCardChoice(kind, NameOf(kind), !_hidden.Contains(kind)));
        }
    }

    private string NameOf(DashboardCardKind kind) => kind switch
    {
        DashboardCardKind.Notes => _translations["Notes"],
        DashboardCardKind.Tasks => _translations["Tasks"],
        DashboardCardKind.Upcoming => _translations["Upcoming"],
        DashboardCardKind.Groups => _translations["Groups"],
        DashboardCardKind.RecentChats => _translations["Recent chats"],
        DashboardCardKind.SharedLocations => _translations["Shared with you"],
        // Named one by one rather than behind a fallback: the fallback quietly called the next card
        // added "Contacts" in the menu that turns cards on and off.
        _ => _translations["Contacts"]
    };

    /// <summary>The cards as built, before pinning moves any of them.</summary>
    private readonly List<DashboardCard> _built = [];

    private void ShowCards()
    {
        Cards.Clear();
        // Put-away parts are dropped here rather than never built: the menu has to be able to bring one
        // back without reloading everything from the store.
        foreach (var card in _built.Where(card => !_hidden.Contains(card.Kind)).OrderByDescending(card => card.IsPinned))
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
            // "Thursday, 27 August", as Orbit.Web's today strip opens - it says what "today" means
            // before saying what is in it.
            today.ToString("dddd, d MMMM", _translations.DisplayCulture),
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
            .Select(note => new DashboardRow(
                note.LocalId, NameOf(note.IsSealed, note.Title, _translations["Untitled"]), Ago(note.UpdatedAtUtc))
            {
                // Badged like a task list, and like the same row on Orbit.Web - only where it says
                // something, which is never for the Normal most notes are.
                Priority = Tasks.PriorityChoice.For(note.Priority, _translations) is { IsWorthSaying: true } priority
                    ? priority.Name
                    : string.Empty
            })
            .ToList();

    private IReadOnlyList<DashboardRow> DescribeTaskLists(IReadOnlyList<LocalTaskList> taskLists)
        => taskLists
            .Where(CanBeShown)
            .OrderByDescending(list => list.IsPinned)
            .ThenByDescending(list => list.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(list => new DashboardRow(
                list.LocalId,
                NameOf(list.IsSealed, _translations.Written(list.Title), _translations["Untitled list"]),
                DescribeProgress(list))
            {
                HasProgress = list.Items.Count > 0,
                Progress = MeasureProgress(list),
                Priority = Tasks.PriorityChoice.For(list.Priority, _translations) is { IsWorthSaying: true } priority
                    ? priority.Name
                    : string.Empty
            })
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
                DescribeWhen(calendarEvent.Details.StartUtc, calendarEvent.Details.IsAllDay))
            {
                // The dot Orbit.Web draws here too, in the event's own colour.
                HasColourDot = true,
                Colour = calendarEvent.Details.Color,
                // And the badge it draws on this card's rows, on the same terms as the other two cards.
                Priority = Tasks.PriorityChoice.For(calendarEvent.Details.Priority, _translations)
                    is { IsWorthSaying: true } priority
                    ? priority.Name
                    : string.Empty
            })
            .ToList();

    /// <summary>Who was last talking, most recent first, with anybody waiting on an answer at the top.</summary>
    /// <summary>
    /// Who is sharing where they are, and whether it keeps coming or was sent once - the same two words
    /// Orbit.Web uses. Tapping one opens the map, which is where a position can actually be looked at.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeSharedPositions(IReadOnlyList<ReceivedPosition> shared)
        => [.. shared
            .Take(RowsPerCard)
            .Select(position => new DashboardRow(
                position.SharerUserId,
                position.SharerDisplayName,
                _translations[position.IsContinuous ? "live" : "sent once"]))];

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

    /// <summary>
    /// The same fraction Orbit.Web fills its bar to. Zero for a list with no entries, where the bar is
    /// not drawn at all - see DashboardRow.HasProgress for why an empty list gets no bar rather than an
    /// empty one.
    /// </summary>
    private static double MeasureProgress(LocalTaskList list)
        => list.Items.Count == 0
            ? 0
            : (double)list.Items.Count(item => item.IsCompleted) / list.Items.Count;

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

    /// <summary>
    /// What to call something the reader may not be able to read. A sealed item has no title to show -
    /// it is sealed with the rest of it - and calling it "Untitled" would claim it has none, which is a
    /// different thing entirely.
    /// </summary>
    private string NameOf(bool isSealed, string title, string placeholder)
        => isSealed ? _translations["Private"] : TitleOrPlaceholder(title, placeholder);
}
