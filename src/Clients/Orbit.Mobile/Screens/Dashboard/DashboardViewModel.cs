using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Folders;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Core.Permissions;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Location;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Security;
using Orbit.Mobile.Screens.Folders;
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
    private readonly LocalInventoryRepository _inventories;
    private readonly LocalPlaceRepository _places;
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
    private readonly LocalNotificationRepository _notifications;
    private readonly IScreenNavigator _navigator;

    /// <summary>The account's tag colours - see LocalTagColourRepository. Null in a test that is not about them.</summary>
    private readonly LocalTagColourRepository? _tagColours;

    [ObservableProperty]
    private TodaySummary _today = TodaySummary.Nothing;

    [ObservableProperty]
    private bool _hasNothing;

    /// <summary>
    /// Every part has been put away, which is not the same as having nothing - and telling somebody
    /// with a full account to "add a note to get started" is telling them the app has lost their work.
    /// Orbit.Web says which of the two it is in the same place; the phone said the first for both.
    /// </summary>
    [ObservableProperty]
    private bool _everythingIsHidden;

    public DashboardViewModel(
        LocalNoteRepository notes, LocalTaskListRepository taskLists,
        LocalCalendarEventRepository calendarEvents, LocalInventoryRepository inventories,
        LocalPlaceRepository places,
        ChatRepository chat, TimeProvider timeProvider,
        Translations translations, PrivateItemGate privateItems, EverythingSynchronizer synchronizer,
        SyncState syncState, UserPermissions permissions, IDashboardPinStore pins,
        IDashboardCardPreferenceStore visibility, SharedLocations sharedLocations,
        LocalNotificationRepository notifications, IScreenNavigator navigator,
        LocalFolderRepository folders, IChosenFolderStore chosenFolder,
        LocalTagColourRepository? tagColours = null)
    {
        _tagColours = tagColours;
        Folders = new FolderTabs(folders, chosenFolder, translations, FolderPage.Dashboard);
        _notes = notes;
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _inventories = inventories;
        _places = places;
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
        _order = visibility.ReadOrder();
        _sharedLocations = sharedLocations;
        _notifications = notifications;
        _navigator = navigator;
    }

    public ObservableCollection<DashboardCard> Cards { get; } = [];

    /// <summary>
    /// The folders this screen offers and which of them is being read - see FolderTabs. The dashboard
    /// draws **both** pages' tabs, because it shows both kinds of card, and offers no way to make one:
    /// there is no dashboard card to file into a folder, so a folder made here would be a tab nothing
    /// could ever go in. See FolderPages.
    /// </summary>
    public FolderTabs Folders { get; }

    /// <inheritdoc cref="Notes.NotesViewModel.FolderChoices"/>
    public ObservableCollection<FolderChoice> FolderChoices { get; } = [];

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
        var inventories = await _inventories.GetAllAsync(cancellationToken);
        // Behind the permission that draws a map at all, the way the shared positions below are: a place
        // is a point, and an account that may not be shown a map has nowhere to put one.
        var places = _permissions.Has(ApplicationPermission.Location)
            ? await _places.GetAllAsync(cancellationToken)
            : [];
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

        // Where the bell is pointing, read once for the whole page. Every card and every row then asks
        // the same set whether any of it means them - see UnreadNews, which Orbit.Web's dashboard asks
        // the same question of.
        _unreadUrls = UnreadNews.AddressesIn(await _notifications.GetUnreadAsync(cancellationToken));

        // Kept current rather than filled when the menu opens: the menu is a panel the page hands over
        // the moment its three dots are pressed, and one filled on the way past would be empty the
        // first time.
        ShowCardChoices();

        Today = SummariseToday(taskLists, events, contacts);

        _built.Clear();
        // An empty card is worse than no card: it takes up a phone's screen to say nothing. Each is
        // added only when it has something in it, which is also how the web's dashboard behaves.
        // Filtered before both the rows and the count, so a card that says "3" is showing three - the
        // same as Orbit.Web, whose count is of what it is about to draw rather than of everything. What
        // the filter itself empties is the exception - see AddCardIfAnything.
        // Which folder each of the two kinds is in, and how many are in each - counted over both, since
        // the tabs here are both pages' at once. No Finished among them: this screen has no such tab,
        // so a finished list is placed by its folder and its privacy like anything else rather than
        // falling out of every tab the screen draws - see FolderPages.HasAFinishedTab.
        await Folders.ReadAsync(cancellationToken);

        FolderChoices.Clear();
        foreach (var choice in Folders.Describe(
            notes.Select(note => Folders.Where(note.FolderId, note.IsPrivate, isFinished: false))
                .Concat(taskLists.Select(list => Folders.Where(list.FolderId, list.IsPrivate, list.IsCompleted)))))
        {
            FolderChoices.Add(choice);
        }

        notes = [.. notes.Where(note => Folders.Holds(Folders.Where(note.FolderId, note.IsPrivate, isFinished: false)))];
        taskLists = [.. taskLists.Where(list => Folders.Holds(Folders.Where(list.FolderId, list.IsPrivate, list.IsCompleted)))];

        var shownNotes = notes.Where(note => Passes(DashboardCardKind.Notes, note.IsPinned)).ToList();
        var shownTaskLists = taskLists.Where(list => Passes(DashboardCardKind.Tasks, list.IsPinned)).ToList();
        var shownEvents = events.Where(PassesPriority).ToList();

        // The account's tag colours, read from this phone like everything else on the page, for the two
        // cards whose rows draw tags - see DashboardRow.Tags.
        var tagColours = _tagColours is null ? null : await _tagColours.ColoursAsync(cancellationToken);

        // Gated on whether the card has anything at all, not on what survives its filter - the same
        // rule Orbit.Web settled on, and for the reason its own comment gives: a card narrowed to
        // nothing would take its filter menu off the page with it, so the choice that emptied it could
        // not be undone from the page that made it.
        AddCardIfAnything(
            DashboardCardKind.Notes, _translations["Notes"], DescribeNotes(shownNotes, tagColours), shownNotes.Count(CanBeShown),
            notes.Any(CanBeShown));
        AddCardIfAnything(
            DashboardCardKind.Tasks, _translations["Tasks"], DescribeTaskLists(shownTaskLists, tagColours), shownTaskLists.Count(CanBeShown),
            taskLists.Any(CanBeShown));
        AddCardIfAnything(
            DashboardCardKind.Upcoming, _translations["Upcoming"], DescribeEvents(shownEvents), shownEvents.Count,
            events.Count > 0);
        // Between what is coming up and who is around, which is where Orbit.Web puts it. The card
        // rather than a row carries the news: something about to go off says "/inventory" and names no
        // shelf (InventoryExpiryPushContent), so there is nothing here that could say which.
        AddCardIfAnything(
            DashboardCardKind.Inventories, _translations["Inventory"], DescribeInventories(inventories),
            inventories.Count(CanBeShown), ownNews: UnreadNews.About(_unreadUrls, "/inventory"));
        AddCardIfAnything(
            DashboardCardKind.Places, _translations["Places you keep"], DescribePlaces(places), places.Count);
        AddCardIfAnything(DashboardCardKind.Groups, _translations["Groups"], DescribeGroups(groups), groups.Count);
        // The one card whose news no row can carry: a position points at "/map" and names nobody
        // (SharedItemNotifier.UrlFor), so the card says it over the lot - which is Orbit.Web's answer
        // on the same card, for the same reason.
        AddCardIfAnything(
            DashboardCardKind.SharedLocations, _translations["Shared with you"],
            DescribeSharedPositions(sharedPositions), sharedPositions.Count,
            ownNews: UnreadNews.About(_unreadUrls, "/map"));
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

            case DashboardCardKind.Inventories:
                _navigator.ShowInventory(row.LocalId);
                break;

            case DashboardCardKind.Places:
                _navigator.ShowPlace(row.LocalId);
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
    /// <param name="anythingAtAll">
    /// Whether the card has anything before its filter is applied. Null where the card has no filter to
    /// apply, which is the same question as whether it has rows.
    /// </param>
    /// <param name="ownNews">
    /// News the card carries itself, for a card whose rows cannot carry any. Everywhere else the card's
    /// mark is simply whether one of its rows has one - a card that says something happened and has no
    /// row saying where leaves the reader to open all of them.
    /// </param>
    private void AddCardIfAnything(
        DashboardCardKind kind, string title, IReadOnlyList<DashboardRow> rows, int total,
        bool? anythingAtAll = null, bool ownNews = false)
    {
        // An empty card is worth its place on a phone's screen only where a filter is what emptied it,
        // because the way to widen that filter again is in its own header.
        if (!(anythingAtAll ?? rows.Count > 0))
        {
            return;
        }

        var ruled = rows
            .Select((row, position) => row with { HasDividerUnder = position < rows.Count - 1 })
            .ToList();
        _built.Add(new DashboardCard(kind, title, total.ToString(), ruled, _pins.Read().Contains(kind))
        {
            CanBeFiltered = OptionsFor(kind).Count > 0,
            HasUnseenAction = ownNews || ruled.Any(row => row.HasNews)
        });
    }

    /// <summary>
    /// Whether a pinnable thing survives its card's filter. "Pinned" is the only filter these cards
    /// offer, so anything else lets everything through.
    /// </summary>
    /// <summary>
    /// Whether this card is about the folder being read. Opening one somebody made is asking to see one
    /// kind of thing - a folder called "Receipts" holds notes, so opening it leaves the notes card
    /// standing and nothing else. Every other card is about something the folder cannot hold, and a
    /// screen that kept drawing them would answer "show me this folder" with the whole dashboard and
    /// one card narrowed inside it.
    ///
    /// The built-in tabs are not about one kind and change nothing here: Public and Private are what
    /// everything is in unless it was filed somewhere. And a folder whose scope this screen cannot read
    /// - one deleted on its own screen while the dashboard held it open - narrows to nothing rather
    /// than quietly to the task lists; FolderTabs.ReadAsync drops it on the next pass.
    /// </summary>
    private bool IsAboutTheOpenFolder(DashboardCardKind kind)
        => Folders.Chosen.FolderId is null
            || Folders.ChosenScope switch
            {
                FolderScope.Notes => kind is DashboardCardKind.Notes,
                FolderScope.Tasks => kind is DashboardCardKind.Tasks,
                _ => false
            };

    /// <inheritdoc cref="Notes.NotesViewModel.ChooseFolderAsync"/>
    [RelayCommand]
    private async Task ChooseFolderAsync(FolderKey key, CancellationToken cancellationToken)
    {
        Folders.Choose(key);
        await ShowStoredSummaryAsync(cancellationToken);
    }

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

    /// <summary>Where everything unread points, so a card or a row can ask whether any of it means it.</summary>
    private IReadOnlyList<string> _unreadUrls = [];

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
        DashboardCardKind.Inventories => _translations["Inventory"],
        DashboardCardKind.Places => _translations["Places you keep"],
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
        var shown = _built.Where(card => !_hidden.Contains(card.Kind)).Where(card => IsAboutTheOpenFolder(card.Kind));

        // Pinned first, always - a pin is the reader saying "this one, above the rest", and an order
        // that moved it back down would be answering a question they did not ask. The same rule the
        // list screens follow, said in the same words - see ListSortOrder.
        var ordered = shown.OrderByDescending(card => card.IsPinned);

        foreach (var card in Order is DashboardCardOrder.Name
            ? ordered.ThenBy(card => card.Title, StringComparer.CurrentCultureIgnoreCase)
            : ordered)
        {
            Cards.Add(card);
        }

        EverythingIsHidden = Cards.Count == 0 && _built.Count > 0;
        HasNothing = Cards.Count == 0 && !EverythingIsHidden;
    }

    /// <summary>
    /// What order the cards are in under the pins. Orbit's own by default, which is the order Orbit.Web
    /// lays them out in and the order a reader learns the page by; by name for somebody who would
    /// rather look one up than remember where it sits.
    /// </summary>
    [ObservableProperty]
    private DashboardCardOrder _order;

    /// <summary>Chosen from the menu under the screen's name, and written down as it is chosen.</summary>
    [RelayCommand]
    private void Arrange(DashboardCardOrder order)
    {
        Order = order;
        _visibility.WriteOrder(order);
        ShowCards();
    }

    /// <summary>
    /// Opens the section a card is a way into, which is what its heading is for - the dashboard shows
    /// the few most relevant rows and the section itself has the rest. The same destinations Orbit.Web's
    /// card headings lead to; the two chat cards both lead to Contacts there, because that page holds
    /// the chats and the directory as tabs.
    /// </summary>
    [RelayCommand]
    private void OpenSection(DashboardCard? card)
    {
        switch (card?.Kind)
        {
            case DashboardCardKind.Notes:
                _navigator.ShowNotes();
                break;
            case DashboardCardKind.Tasks:
                _navigator.ShowTasks();
                break;
            case DashboardCardKind.Upcoming:
                _navigator.ShowCalendar();
                break;
            case DashboardCardKind.Inventories:
                _navigator.ShowInventory();
                break;
            case DashboardCardKind.Places:
                _navigator.ShowPlaces();
                break;
            case DashboardCardKind.Groups:
                _navigator.ShowGroups();
                break;
            case DashboardCardKind.RecentChats:
            case DashboardCardKind.Contacts:
                _navigator.ShowContacts();
                break;
            case DashboardCardKind.SharedLocations:
                _navigator.ShowMap();
                break;
        }
    }

    /// <summary>
    /// Where the strip of today's counts leads. It is a summary of a day, and the page that shows a day
    /// is the calendar - which is what Orbit.Web's own today strip is a button for.
    /// </summary>
    [RelayCommand]
    private void OpenCalendar() => _navigator.ShowCalendarDay();

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

    private bool CanBeShown(LocalInventory inventory) => !inventory.IsPrivate || _privateItems.IsUnlocked;

    private TodaySummary SummariseToday(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyList<LocalCalendarEvent> events,
        IReadOnlyList<LocalContact> contacts)
    {
        var now = _timeProvider.GetUtcNow();
        var today = now.Date;
        var dueToday = EntriesDueOn(taskLists, today);
        var eventsToday = events
            .Where(calendarEvent => calendarEvent.Details.StartUtc.Date == today)
            .ToList();

        return new TodaySummary(
            // "Thursday, 27 August", as Orbit.Web's today strip opens - it says what "today" means
            // before saying what is in it.
            today.ToString("dddd, d MMMM", _translations.DisplayCulture),
            dueToday.Count,
            eventsToday.Count,
            // Only requests waiting on the reader. One they sent and nobody has answered is not
            // something they can act on, so counting it would be asking them to do nothing.
            contacts.Count(contact => contact.RequiresApprovalFromCurrentUser),
            dueToday.Count(item => item.IsCompleted),
            eventsToday.Count(calendarEvent => calendarEvent.Details.EndUtc <= now));
    }

    /// <summary>
    /// The day's entries, done and not - what the strip counts over. It used to count only what was
    /// still owed, which answered "how much is there" and never "how far through it am I": a day whose
    /// work was all ticked off read "0 tasks due today", which is three tasks that disappeared rather
    /// than three that were done. Orbit.Web's own strip changed the same way.
    ///
    /// A list closed <b>while work was still unticked on it</b> owes nothing, whatever is left on it -
    /// see CalendarDeadline. Asked that way round rather than of IsCompleted alone, which is also true
    /// of a list whose entries simply all got ticked, and those are exactly the entries the fraction
    /// exists to show.
    /// </summary>
    private static IReadOnlyList<Orbit.Contracts.Tasks.TaskItemDto> EntriesDueOn(
        IReadOnlyList<LocalTaskList> taskLists, DateTime day)
        => [.. taskLists
            .Where(list => !(list.IsCompleted && list.Items.Any(item => !item.IsCompleted && !item.IsFailed)))
            .SelectMany(list => list.Items)
            .Where(item => item.DueDateUtc?.Date == day)];

    /// <summary>
    /// A private note's title is the thing the gate hides, and the dashboard shows titles - so leaving
    /// it out here would have hidden a note on its own screen and named it on the landing one. Found by
    /// walking the app: the gate was locked and the title was on the dashboard.
    /// </summary>
    /// <param name="tagColours">The account's tag colours by key - see LocalTagColourRepository.ColoursAsync. Null draws every tag plain.</param>
    private IReadOnlyList<DashboardRow> DescribeNotes(
        IReadOnlyList<LocalNote> notes, IReadOnlyDictionary<string, string>? tagColours)
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
                    : string.Empty,
                // A private one still locked never gets this far (CanBeShown); a sealed one has its tags
                // sealed with everything else it says, so there is nothing to draw.
                Tags = note.IsSealed
                    ? Screens.Tags.TagChips.None
                    : Screens.Tags.TagChips.For(note.AllTags, tagColours)
            })
            .ToList();

    /// <inheritdoc cref="DescribeNotes"/>
    private IReadOnlyList<DashboardRow> DescribeTaskLists(
        IReadOnlyList<LocalTaskList> taskLists, IReadOnlyDictionary<string, string>? tagColours)
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
                    : string.Empty,
                // A deadline and an overdue entry both point at the list they sit on
                // (DailyTaskReminderPushContent, OverdueTaskPushContent), so the row that names that
                // list is the one that can say so. A list the server has never seen has no address.
                HasNews = list.ServerId is { } serverId && UnreadNews.About(_unreadUrls, $"/tasks/{serverId}"),
                // As on the notes card, and for the same reason nothing for a sealed list.
                Tags = list.IsSealed
                    ? Screens.Tags.TagChips.None
                    : Screens.Tags.TagChips.For(list.AllTags, tagColours)
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
                    : string.Empty,
                // A reminder is the event's own and says so - see EventReminderPushContent.
                HasNews = calendarEvent.ServerId is { } serverId
                    && UnreadNews.About(_unreadUrls, $"/calendar/{serverId}")
            })
            .ToList();

    /// <summary>
    /// The shelves, newest change first, each saying what is on it and whether it is one this reader
    /// keeps to themselves or one somebody handed over - the two words Orbit.Web badges the same rows
    /// with. A sealed shelf is named "Private" rather than "Untitled": it has a name, and this device
    /// cannot read it.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeInventories(IReadOnlyList<LocalInventory> inventories)
        => inventories
            .OrderByDescending(inventory => inventory.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Where(CanBeShown)
            .Select(inventory => new DashboardRow(
                inventory.LocalId,
                NameOf(inventory.IsSealed, inventory.Name, _translations["Untitled"]),
                DescribeShelf(inventory)))
            .ToList();

    /// <summary>
    /// What a shelf says for itself at a glance. How much is on it first, because that is what the card
    /// is for; then the one thing worth knowing about who it belongs to, where there is one.
    /// </summary>
    private string DescribeShelf(LocalInventory inventory)
    {
        var count = _translations.Format("Items: {0}", inventory.Items.Count);

        return inventory switch
        {
            { IsShared: true } => $"{count} · {_translations["Shared"]}",
            { IsPrivate: true } => $"{count} · {_translations["Private"]}",
            _ => count
        };
    }

    /// <summary>
    /// The places kept, newest change first, each saying where it is and - where somebody handed it over
    /// - who from. The address rather than the point: a list of coordinates is a list nobody reads.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribePlaces(IReadOnlyList<LocalPlace> places)
        => places
            .OrderByDescending(place => place.UpdatedAtUtc)
            .Take(RowsPerCard)
            .Select(place => new DashboardRow(place.LocalId, place.Name, DescribePlace(place)))
            .ToList();

    private string DescribePlace(LocalPlace place)
        => place switch
        {
            { IsShared: true, SharedByUserName: { Length: > 0 } sharer } => place.Address.Length > 0
                ? $"{place.Address} · {_translations.Format("From {0}", sharer)}"
                : _translations.Format("From {0}", sharer),
            _ => place.Address
        };

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
                _translations[position.IsContinuous ? "live" : "sent once"])
            {
                // Whose position it is, drawn as they are drawn everywhere else. No dot: the row is
                // about a pin on the map, not about whether they are around to be written to.
                HasAvatar = true
            })];

    private IReadOnlyList<DashboardRow> DescribeRecentChats(IReadOnlyList<LocalContact> contacts)
        => contacts
            .OrderByDescending(contact => contact.RequiresApprovalFromCurrentUser)
            .ThenByDescending(contact => contact.LastMessageAtUtc)
            .Take(RowsPerCard)
            .Select(contact => new DashboardRow(
                contact.UserId,
                contact.DisplayName,
                contact.RequiresApprovalFromCurrentUser ? _translations["Wants to chat"] : Ago(contact.LastMessageAtUtc))
            {
                // Orbit.Web marks this row from the unread count it keeps per conversation; the phone's
                // contact row carries no such count, and a message notification points at whoever sent
                // it (ChatMessagePushContent), which is the same fact read off what the phone has.
                HasNews = UnreadNews.About(_unreadUrls, $"/chat/{contact.UserId}"),
                HasAvatar = true,
                Presence = contact.PresenceStatus
            })
            .ToList();

    /// <summary>
    /// A plain directory, alphabetical. Leaves out conversations nobody has answered yet, so an
    /// unanswered request shows up once - in Recent chats - rather than in both.
    /// </summary>
    private IReadOnlyList<DashboardRow> DescribeDirectory(IReadOnlyList<LocalContact> contacts)
        => DirectoryOf(contacts)
            .Take(RowsPerCard)
            .Select(contact => new DashboardRow(contact.UserId, contact.DisplayName, string.Empty)
            {
                HasAvatar = true,
                Presence = contact.PresenceStatus
            })
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
            .Select(group => new DashboardRow(group.Id, group.Name, string.Empty)
            {
                // An invitation points at the group it is to - see ChatGroupInvitationPushContent.
                HasNews = UnreadNews.About(_unreadUrls, $"/chat/groups/{group.Id}"),
                // The circle, but never a presence dot on it: a group is not somewhere anybody is.
                HasAvatar = true
            })
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
