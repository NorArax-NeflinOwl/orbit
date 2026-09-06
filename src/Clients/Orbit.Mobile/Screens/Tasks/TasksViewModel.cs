using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Task lists, read from the local database exactly as notes are - the second feature on the sync spine
/// and, deliberately, the same shape of screen, so what differs is only what genuinely differs.
/// </summary>
public sealed partial class TasksViewModel : ObservableObject
{
    private readonly LocalTaskListRepository _taskLists;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly TasksClient _tasksClient;
    private readonly INetworkStatus _networkStatus;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;
    private readonly Translations _translations;
    private readonly ITaskListArrangementStore _arrangements;
    private readonly PrivateItemGate _privateItems;

    [ObservableProperty]
    private string _newListTitle = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>The one thing this screen has to say for itself, which today is only about pinning.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>The status being filtered to, or null for all of them - see TaskListView.</summary>
    [ObservableProperty]
    private string? _statusFilter;

    /// <summary>
    /// What is being looked for among the entries on every list - see TaskItemFilter. Its own object
    /// because the search box and the chips are one question asked two ways.
    /// </summary>
    private readonly TaskItemFilter _itemFilter = new();

    /// <summary>A word from an entry, on any list. Everything is shown until something is typed.</summary>
    public string ItemSearch
    {
        get => _itemFilter.Search;
        set
        {
            if (_itemFilter.Search == value)
            {
                return;
            }

            _itemFilter.Search = value;
            OnPropertyChanged();
            ShowArrangedLists();
        }
    }

    /// <inheritdoc cref="TaskItemFilter.MatchesEveryCategory"/>
    public bool MatchesEveryCategory
    {
        get => _itemFilter.MatchesEveryCategory;
        set
        {
            if (_itemFilter.MatchesEveryCategory == value)
            {
                return;
            }

            _itemFilter.MatchesEveryCategory = value;
            OnPropertyChanged();
            ShowArrangedLists();
        }
    }

    /// <summary>Whether anything has been asked for, which is what makes "clear" worth offering.</summary>
    public bool IsLookingForAnEntry => _itemFilter.IsActive;

    /// <summary>
    /// What an empty screen means, which is not the same thing twice: a page narrowed away by a search
    /// is changed by typing something else, and one with nothing on it at all by making a list. Saying
    /// "no task lists yet" to somebody holding six of them reads as having lost them.
    /// </summary>
    public string NothingHereMessage => _itemFilter.IsActive
        ? _translations["Nothing on any list matches that."]
        : _translations["No task lists yet."];

    /// <summary>
    /// Only worth asking once two are chosen: with one, "any of them" and "all of them" are the same
    /// question.
    /// </summary>
    public bool IsCategoryRuleWorthAsking => _itemFilter.Categories.Count > 1;

    /// <summary>
    /// Every category in use on this account, with how many entries each would leave. Built from what
    /// the phone holds rather than asked of the server: the same lists the screen is showing are the
    /// ones the answer is about.
    /// </summary>
    public ObservableCollection<TaskCategoryChoice> Categories { get; } = [];

    public bool HasCategories => Categories.Count > 0;

    /// <summary>Read back from where the reader left it - see ITaskListArrangementStore.</summary>
    private TaskListArrangement _arrangement;

    /// <summary>The cards folded down to their heading - see ToggleCollapsed.</summary>
    private readonly HashSet<Guid> _collapsed;

    public TasksViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, TasksClient tasksClient,
        INetworkStatus networkStatus, ITaskListArrangementStore arrangements, PrivateItemGate privateItems,
        SyncState syncState, IScreenNavigator navigator, Translations translations)
    {
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _tasksClient = tasksClient;
        _networkStatus = networkStatus;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
        _arrangements = arrangements;
        _privateItems = privateItems;
        _arrangement = new TaskListArrangement(arrangements.ReadSortOrder(), arrangements.ReadManualOrder());
        _collapsed = [.. arrangements.ReadCollapsed()];
    }

    public ObservableCollection<TaskListRow> TaskLists { get; } = [];

    /// <summary>What the cards are sorted by - the half of the arrangement a reader chooses directly.</summary>
    public TaskListSortOrder SortOrder => _arrangement.SortOrder;

    /// <summary>
    /// Shows what is already on the phone first, then synchronises - the other order leaves the screen
    /// blank for a round trip, and empty for as long as there is no network at all.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowStoredListsAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddList))]
    private async Task AddListAsync(CancellationToken cancellationToken)
    {
        await _taskLists.CreateAsync(NewListTitle.Trim(), TaskListRow.NoItems, cancellationToken);
        NewListTitle = string.Empty;

        await ShowStoredListsAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private bool CanAddList => NewListTitle.Trim().Length > 0;

    /// <summary>
    /// Opens one list. A hidden row opens nothing: it offers the lock instead, which is the whole point
    /// of hiding it - see TaskListRow.CanBeOpened.
    /// </summary>
    [RelayCommand]
    private void OpenList(TaskListRow? row)
    {
        if (row is { CanBeOpened: true })
        {
            _navigator.ShowTaskList(row.LocalId);
        }
    }

    /// <summary>
    /// Getting rid of a list from its own card, which is what Orbit.Web's Tasks card offers. Only for
    /// a list this reader owns - a shared one is somebody else's, and the card leaves the entry out
    /// rather than offering a press that would be refused.
    ///
    /// What the browser asks second - whether the other lists a group list gathers should go too - is
    /// not asked here, because the phone's own delete cannot carry the answer: the local store takes
    /// one list at a time. The group list goes and what it gathered stays, which is the browser's
    /// answer when somebody cancels that second question.
    ///
    /// Asking at all is the page's job, not this one's: what a question looks like is a screen's
    /// business, and there is nothing here to ask with.
    /// </summary>
    [RelayCommand]
    private async Task DeleteListAsync(TaskListRow? row, CancellationToken cancellationToken)
    {
        if (row is not { IsSharedWithMe: false })
        {
            return;
        }

        var outcome = await _taskLists.DeleteAsync(row.LocalId, cancellationToken);
        if (outcome.WasRefused())
        {
            Message = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        Message = string.Empty;
        await ShowStoredListsAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// The dictionary key, not the text itself - see <see cref="Translations"/>. The same sentence the
    /// list's own screen uses, because it is the same refusal about the same list.
    /// </summary>
    private const string RefusalMessage =
        "Somebody else can change this list, and Orbit can't be reached to check. It stays read-only until you're back online.";

    /// <inheritdoc cref="NotesViewModel.UnlockPrivateAsync"/>
    [RelayCommand]
    private async Task UnlockPrivateAsync(CancellationToken cancellationToken)
    {
        if (await _privateItems.TryUnlockAsync(cancellationToken))
        {
            ShowArrangedLists();
        }
    }

    /// <inheritdoc cref="NotesViewModel.TogglePinAsync"/>
    [RelayCommand]
    private async Task TogglePinAsync(TaskListRow? row, CancellationToken cancellationToken)
    {
        if (row is null || await _taskLists.FindAsync(row.LocalId, cancellationToken) is not { ServerId: { } serverId })
        {
            return;
        }

        try
        {
            if (await _tasksClient.SetPinnedAsync(serverId, !row.IsPinned, cancellationToken) is not WriteOutcome.Applied)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Pinning needs a connection."];
            return;
        }

        await _taskLists.MarkPinnedAsync(row.LocalId, !row.IsPinned, cancellationToken);
        Message = string.Empty;
        await ShowStoredListsAsync(cancellationToken);
    }

    private async Task ShowStoredListsAsync(CancellationToken cancellationToken)
    {
        var stored = await _taskLists.GetAllAsync(cancellationToken);
        var pending = await _taskLists.GetPendingLocalIdsAsync(cancellationToken);

        _stored = stored;
        _pending = pending;
        ShowArrangedLists();
    }

    private IReadOnlyList<LocalTaskList> _stored = [];
    private IReadOnlySet<Guid> _pending = new HashSet<Guid>();

    /// <summary>
    /// Re-arranges what is already held rather than re-reading it. Choosing a filter is a question about
    /// the same lists, so asking the database again would be a round trip to learn nothing.
    /// </summary>
    private void ShowArrangedLists()
    {
        ShowCategoriesInUse();

        TaskLists.Clear();
        foreach (var taskList in TaskListView.Arrange(_stored, StatusFilter, _arrangement)
            .Where(_itemFilter.HasAMatch))
        {
            // Every list, not just the visible ones: a group's row looks up what its links stand for,
            // and a member filtered off the screen is still where that work sits.
            TaskLists.Add(TaskListRow.From(
                taskList, _stored, _pending.Contains(taskList.LocalId), _networkStatus, _translations,
                _privateItems.IsUnlocked, _translations["Private"])
                with
                {
                    CanBeMoved = SortOrder == TaskListSortOrder.Manual,
                    IsCollapsed = _collapsed.Contains(taskList.LocalId),
                    FoldDescription = _collapsed.Contains(taskList.LocalId)
                        ? _translations["Expand"]
                        : _translations["Collapse"],
                    // What answered, in place of what the card would otherwise say is next: a list left
                    // on screen for a match nobody can see reads as a bug.
                    Matched = _itemFilter.FirstMatch(taskList) is { } match
                        ? _translations.Written(match.Description)
                        : string.Empty
                });
        }

        OnPropertyChanged(nameof(SortDescription));

        // The chips carry the count of what each would leave, and that count changes with what is held -
        // not only with which chip is chosen. Raised only when one was tapped, every chip read "0" from
        // the first paint until the reader tapped one, on a screen that was already showing six lists.
        OnPropertyChanged(nameof(Filters));
        OnPropertyChanged(nameof(IsLookingForAnEntry));
        OnPropertyChanged(nameof(NothingHereMessage));
        OnPropertyChanged(nameof(IsCategoryRuleWorthAsking));
    }

    /// <summary>
    /// Narrows the screen to the entries filed under one word, or widens it again. Several can be
    /// chosen at once - see TaskItemFilter for what two of them mean together.
    /// </summary>
    [RelayCommand]
    private void ToggleCategory(TaskCategoryChoice? category)
    {
        if (category is null)
        {
            return;
        }

        _itemFilter.Toggle(category.Name);
        ShowArrangedLists();
    }

    /// <summary>Back to every list, whatever was being looked for.</summary>
    [RelayCommand]
    private void ClearItemFilter()
    {
        _itemFilter.Clear();
        OnPropertyChanged(nameof(ItemSearch));
        OnPropertyChanged(nameof(MatchesEveryCategory));
        ShowArrangedLists();
    }

    private void ShowCategoriesInUse()
    {
        var counted = _stored
            .SelectMany(taskList => taskList.Items)
            .SelectMany(item => item.AllCategories)
            .GroupBy(category => category, StringComparer.CurrentCultureIgnoreCase)
            .Select(byCategory => new TaskCategoryChoice(
                byCategory.Key, byCategory.Count(), _itemFilter.IsChosen(byCategory.Key)))
            .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Categories.Clear();
        foreach (var category in counted)
        {
            Categories.Add(category);
        }

        OnPropertyChanged(nameof(HasCategories));
    }

    /// <summary>
    /// Whether there is anything to say. Message was set and never shown: nothing on the page was
    /// bound to it, so pinning with no connection did nothing and said nothing - the failure this
    /// screen writes the message to avoid.
    /// </summary>
    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    /// <summary>What the sort button says, which is what it is currently sorted by.</summary>
    public string SortDescription => TaskListView.Describe(SortOrder, _translations);

    /// <summary>The filter chips, each with the count of what it would leave.</summary>
    public IReadOnlyList<TaskListFilter> Filters
        => [.. new string?[] { null }.Concat(TaskListView.Statuses)
            .Select(status => new TaskListFilter(
                status,
                status is null ? _translations["All"] : TaskListView.Describe(status, _translations),
                status is null ? _stored.Count : _stored.Count(taskList => taskList.Status == status),
                status == StatusFilter))];

    [RelayCommand]
    private void FilterBy(TaskListFilter? filter)
    {
        StatusFilter = filter?.Status;
        ShowArrangedLists();
    }

    /// <summary>Every order, the one in force marked - what the sort button opens.</summary>
    public IReadOnlyList<TaskListSortChoice> SortChoices
        => [.. Enum.GetValues<TaskListSortOrder>()
            .Select(order => new TaskListSortChoice(
                order, TaskListView.Describe(order, _translations), order == SortOrder))];

    /// <summary>
    /// A menu rather than a button stepping through them, as it was when there were fewer: six orders
    /// is five taps to undo a mistaken one, and the reader cannot see what they are stepping towards.
    /// </summary>
    [RelayCommand]
    private void ChooseSortOrder(TaskListSortChoice? choice)
    {
        if (choice is null)
        {
            return;
        }

        _arrangement = _arrangement with { SortOrder = choice.Order };

        // Written at once rather than on the way out: there is no moment a screen is told it is leaving
        // for good, and an order that took a restart to stick would read as one that had not.
        _arrangements.WriteSortOrder(SortOrder);
        ShowArrangedLists();
    }

    /// <summary>
    /// Moves a card one place in the reader's own order. Orbit.Web drags them; a phone offers up and
    /// down, which is a target a thumb can hit in a scrolling list - the same answer the checklist
    /// screen gives for the entries on it.
    /// </summary>
    [RelayCommand]
    private void MoveListUp(TaskListRow? row) => MoveList(row, by: -1);

    [RelayCommand]
    private void MoveListDown(TaskListRow? row) => MoveList(row, by: 1);

    private void MoveList(TaskListRow? row, int by)
    {
        var from = row is null ? -1 : IndexOfVisible(row.LocalId);
        var to = from + by;

        // The ends are where the screen stops, not a failure: the first card has nowhere above it.
        if (from < 0 || to < 0 || to >= TaskLists.Count)
        {
            return;
        }

        PutBeside(row!.LocalId, TaskLists[to].LocalId, by);
    }

    private int IndexOfVisible(Guid localId)
    {
        for (var index = 0; index < TaskLists.Count; index++)
        {
            if (TaskLists[index].LocalId == localId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Puts one card where its neighbour on screen is, in an order naming every list rather than only
    /// the ones on screen. A filter is on more often than not here - the chips are the first thing under
    /// the heading - and writing back only what is visible would drop everything filtered away to the
    /// end of the reader's arrangement, which is not what moving one card asked for. Orbit.Web writes
    /// back what it can see and loses the rest that way.
    /// </summary>
    private void PutBeside(Guid moving, Guid neighbour, int by)
    {
        var order = TaskListView.Arrange(_stored, status: null, _arrangement)
            .Select(taskList => taskList.LocalId)
            .ToList();

        order.Remove(moving);
        order.Insert(by < 0 ? order.IndexOf(neighbour) : order.IndexOf(neighbour) + 1, moving);

        _arrangement = _arrangement with { ManualOrder = order };
        _arrangements.WriteManualOrder(order);
        ShowArrangedLists();
    }

    /// <summary>
    /// Folds a card down to its heading, or opens it again. Folded rather than filtered away, which is
    /// the distinction Orbit.Web draws too: a list somebody is not working on this week is still one
    /// they want to see is there, and a filter would take it off the screen altogether.
    /// </summary>
    [RelayCommand]
    private void ToggleCollapsed(TaskListRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_collapsed.Remove(row.LocalId))
        {
            _collapsed.Add(row.LocalId);
        }

        _arrangements.WriteCollapsed([.. _collapsed]);
        ShowArrangedLists();
    }
    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            RecordSync(result);

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredListsAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            // The server was reached and refused - an expired session, most often. AppNavigator watches
            // the session store and moves to sign-in when that is what happened.
            _syncState.RecordFailed();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>"Offline" is only said when the phone actually believes it has no connection.</summary>
    /// <summary>
    /// A sync that never reached the server is not the same as one the server refused, and SyncState
    /// tells them apart from the phone's own belief about connectivity rather than from the result.
    /// </summary>
    private void RecordSync(SyncResult result)
    {
        if (result.ReachedTheServer)
        {
            _syncState.RecordSucceeded();
            return;
        }

        _syncState.RecordFailed();
    }
    partial void OnNewListTitleChanged(string value) => AddListCommand.NotifyCanExecuteChanged();
}
