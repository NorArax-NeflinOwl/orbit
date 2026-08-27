using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
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
    private readonly INetworkStatus _networkStatus;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _newListTitle = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public TasksViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, INetworkStatus networkStatus,
        SyncState syncState, IScreenNavigator navigator)
    {
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _syncState = syncState;
        _navigator = navigator;
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

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    private async Task ShowStoredListsAsync(CancellationToken cancellationToken)
    {
        var stored = await _taskLists.GetAllAsync(cancellationToken);
        var pending = await _taskLists.GetPendingLocalIdsAsync(cancellationToken);

        TaskLists.Clear();
        foreach (var taskList in stored)
        {
            TaskLists.Add(TaskListRow.From(taskList, pending.Contains(taskList.LocalId), _networkStatus));
        }
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
