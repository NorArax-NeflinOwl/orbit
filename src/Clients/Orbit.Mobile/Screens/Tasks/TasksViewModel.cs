using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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

    [ObservableProperty]
    private TaskListSortOrder _sortOrder = TaskListSortOrder.Priority;

    public TasksViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, TasksClient tasksClient,
        INetworkStatus networkStatus,
        SyncState syncState, IScreenNavigator navigator, Translations translations)
    {
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _tasksClient = tasksClient;
        _networkStatus = networkStatus;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
    }

    public ObservableCollection<TaskListRow> TaskLists { get; } = [];

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

    [RelayCommand]
    private void OpenList(TaskListRow? row)
    {
        if (row is not null)
        {
            _navigator.ShowTaskList(row.LocalId);
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
        TaskLists.Clear();
        foreach (var taskList in TaskListView.Arrange(_stored, StatusFilter, SortOrder))
        {
            // Every list, not just the visible ones: a group's row looks up what its links stand for,
            // and a member filtered off the screen is still where that work sits.
            TaskLists.Add(TaskListRow.From(
                taskList, _stored, _pending.Contains(taskList.LocalId), _networkStatus, _translations));
        }

        OnPropertyChanged(nameof(SortDescription));
    }

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
        OnPropertyChanged(nameof(Filters));
    }

    /// <summary>Steps through the five orders in turn, which is a button rather than a dropdown on a phone.</summary>
    [RelayCommand]
    private void NextSortOrder()
    {
        SortOrder = SortOrder == TaskListSortOrder.ReverseAlphabetical
            ? TaskListSortOrder.Priority
            : SortOrder + 1;

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
