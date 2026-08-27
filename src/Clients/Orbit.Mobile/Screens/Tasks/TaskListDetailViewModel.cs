using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One task list and its items. Every change is written to the local database first and queued from
/// there, so ticking something off works with no connection and the screen never waits on a request.
///
/// The offline policy is enforced by the store rather than here - see <see cref="LocalWriteOutcome"/> -
/// so this screen's job is to say what happened, not to be the only thing standing between a shared
/// list and an edit that should not have been queued.
/// </summary>
public sealed partial class TaskListDetailViewModel : ObservableObject
{
    private readonly LocalTaskListRepository _taskLists;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly TasksClient _tasksClient;
    private readonly EditLock _editLock;
    private readonly Translations _translations;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private IReadOnlyList<TaskItemDto> _items = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <inheritdoc cref="Inventory.WarehouseDetailViewModel.BeingEdited"/>
    [ObservableProperty]
    private TaskItemEditor? _beingEdited;

    public bool IsEditingItem => BeingEdited is not null;

    public bool IsShowingList => BeingEdited is null;

    public TaskListDetailViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, Translations translations,
        TimeProvider timeProvider, SharePanel share, IScreenNavigator navigator,
        TasksClient tasksClient, EditLock editLock, INetworkStatus networkStatus)
    {
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _translations = translations;
        _timeProvider = timeProvider;
        Share = share;
        _navigator = navigator;
        _tasksClient = tasksClient;
        _editLock = editLock;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
        _networkStatus = networkStatus;
    }

    public ObservableCollection<TaskItemRow> Items { get; } = [];

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredListAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItemAsync(CancellationToken cancellationToken)
    {
        var description = NewItemDescription.Trim();
        NewItemDescription = string.Empty;

        return SaveAsync(
            [.. _items, new TaskItemDto(Guid.Empty, description, null, false, null, "None", false, "None", new TimeOnly(9, 0))],
            cancellationToken);
    }

    private bool CanAddItem => NewItemDescription.Trim().Length > 0;

    /// <summary>Opens one entry's details - when it is due, and what it says when it is late.</summary>
    [RelayCommand]
    private void EditItem(TaskItemRow? row)
    {
        if (row is not null && CanEdit)
        {
            BeingEdited = TaskItemEditor.For(row.Item);
            MoveTarget = null;
            OnPropertyChanged(nameof(CanMoveItem));
        }
    }

    /// <summary>The other lists this entry could go to - see <see cref="TaskListChoice"/>.</summary>
    public ObservableCollection<TaskListChoice> MoveTargets { get; } = [];

    /// <summary>
    /// Choosing one moves the entry there and then, rather than waiting for this form's Save. The move
    /// is not part of the entry - it is a change to two lists - and Orbit.Web's editor does the same.
    /// </summary>
    [ObservableProperty]
    private TaskListChoice? _moveTarget;

    /// <summary>
    /// An entry added on this phone and not yet synced has no id the server would recognise, so there
    /// is nothing to move yet. Offline there is nobody to do the moving at all.
    /// </summary>
    public bool CanMoveItem
        => CanEdit
            && _networkStatus.IsOnline
            && MoveTargets.Count > 0
            && BeingEdited?.ToDto().Id is { } itemId && itemId != Guid.Empty;

    [RelayCommand]
    private async Task MoveItemAsync(TaskListChoice? target, CancellationToken cancellationToken)
    {
        if (target is null
            || BeingEdited?.ToDto().Id is not { } itemId
            || await _taskLists.FindAsync(_localId, cancellationToken) is not { ServerId: { } sourceServerId })
        {
            return;
        }

        BeingEdited = null;

        // Anything queued goes first: the server is about to be asked to rearrange these two lists, and
        // it should be rearranging what the phone last said, not a version behind.
        await SynchroniseAsync(cancellationToken);
        var outcome = await _tasksClient.MoveItemAsync(sourceServerId, itemId, target.ServerId, cancellationToken);

        // Then again, to bring both lists back as the server now has them - said after the sync, which
        // clears the status of its own.
        await SynchroniseAsync(cancellationToken);
        await ShowStoredListAsync(cancellationToken);

        Status = outcome is WriteOutcome.Applied
            ? _translations.Format("Moved to {0}.", target.Name)
            : _translations["Couldn't move it. Try again."];
    }

    private async Task ShowWhereItCanGoAsync(CancellationToken cancellationToken)
    {
        var others = await _taskLists.GetAllAsync(cancellationToken);

        MoveTargets.Clear();
        foreach (var other in others.Where(list => list.LocalId != _localId && list.ServerId is not null))
        {
            MoveTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
        }
    }

    [RelayCommand]
    private void CancelItemEdit() => BeingEdited = null;

    [RelayCommand]
    private Task SaveItemAsync(CancellationToken cancellationToken)
    {
        if (BeingEdited is not { CanSave: true } editor)
        {
            return Task.CompletedTask;
        }

        var edited = editor.ToDto();
        BeingEdited = null;

        return SaveAsync([.. _items.Select(item => item.Id == edited.Id ? edited : item)], cancellationToken);
    }

    [RelayCommand]
    private Task ToggleItemAsync(TaskItemRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync(
                _items.Select(item => item.Id == row.Id ? item with { IsCompleted = !item.IsCompleted } : item).ToList(),
                cancellationToken);

    [RelayCommand]
    private Task RemoveItemAsync(TaskItemRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync(_items.Where(item => item.Id != row.Id).ToList(), cancellationToken);

    [RelayCommand]
    private async Task DeleteListAsync(CancellationToken cancellationToken)
    {
        var outcome = await _taskLists.DeleteAsync(_localId, cancellationToken);
        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowTasks();
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowTasks();

    private async Task SaveAsync(IReadOnlyList<TaskItemDto> items, CancellationToken cancellationToken)
    {
        var outcome = await _taskLists.UpdateAsync(_localId, Title, items, cancellationToken);
        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
            return;
        }

        await ShowStoredListAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task ShowStoredListAsync(CancellationToken cancellationToken)
    {
        if (await _taskLists.FindAsync(_localId, cancellationToken) is not { } taskList)
        {
            _navigator.ShowTasks();
            return;
        }

        Title = taskList.Title;
        if (taskList.ServerId is { } serverId)
        {
            Share.Describes(
                SharedItemKind.TaskList, serverId, taskList.Title,
                taskList.AccessLevel == "CanEdit" ? null : taskList.OwnerUserId);
        }

        _items = taskList.Items;
        await ShowWhereItCanGoAsync(cancellationToken);
        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        IsReadOnly = !await _taskLists.CanEditAsync(_localId, cancellationToken);
        ReadOnlyReason = string.Empty;

        if (!IsReadOnly && taskList.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_tasksClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }

        Items.Clear();
        foreach (var item in taskList.Items)
        {
            Items.Add(TaskItemRow.From(item, _translations, _timeProvider.GetUtcNow()));
        }
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer ? string.Empty : _translations["Saved on this phone - it will sync later"];

            // Re-read rather than keep what was shown before the sync. An entry added here has no server
            // id until the push comes back with one, and a later save built on the older copy would send
            // no id at all - so the server would mint a second entry and cut loose whatever pointed at
            // the first. See TaskItemRequest.Id.
            if (result.Received > 0)
            {
                await ShowStoredListAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this list, and Orbit can't be reached to check. It stays read-only until you're back online.";

    partial void OnBeingEditedChanged(TaskItemEditor? value)
    {
        OnPropertyChanged(nameof(IsEditingItem));
        OnPropertyChanged(nameof(IsShowingList));
        OnPropertyChanged(nameof(CanMoveItem));
    }

    partial void OnMoveTargetChanged(TaskListChoice? value)
    {
        if (value is not null)
        {
            MoveItemCommand.Execute(value);
        }
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnNewItemDescriptionChanged(string value) => AddItemCommand.NotifyCanExecuteChanged();

    /// <summary>Why it cannot be changed right now - empty when it can, which is the common case.</summary>
    [ObservableProperty]
    private string _readOnlyReason = string.Empty;

    public bool HasReadOnlyReason => ReadOnlyReason.Length > 0;

    private void ShowWhoElseIsEditing()
    {
        if (!_editLock.IsHeldByAnother)
        {
            return;
        }

        IsReadOnly = true;
        ReadOnlyReason = _editLock.RefusalMessage;
    }

    /// <summary>Lets it go when the screen does, rather than leaving it claimed for a minute.</summary>
    public Task CloseAsync() => _editLock.ReleaseAsync();

    partial void OnReadOnlyReasonChanged(string value) => OnPropertyChanged(nameof(HasReadOnlyReason));
}
