using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// The notes screen. Reads the local database and never the API - that is what makes it work with no
/// connection, and it is structural rather than an optimisation (info/orbit-maui-plan.md §6). The
/// synchroniser is what brings the two into step, and this screen only ever asks it to run.
/// </summary>
public sealed partial class NotesViewModel : ObservableObject
{
    private readonly LocalNoteRepository _notes;
    private readonly NoteSynchronizer _synchronizer;
    private readonly NotesClient _notesClient;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly PrivateItemGate _privateItems;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;


    [ObservableProperty]
    private string _newNoteTitle = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>The one thing this screen has to say for itself, which today is only about pinning.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    public NotesViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, NotesClient notesClient,
        INetworkStatus networkStatus,
        Translations translations, PrivateItemGate privateItems,
        SyncState syncState, IScreenNavigator navigator)
    {
        _notes = notes;
        _synchronizer = synchronizer;
        _notesClient = notesClient;
        _networkStatus = networkStatus;
        _translations = translations;
        _privateItems = privateItems;
        _syncState = syncState;
        _navigator = navigator;
    }

    public ObservableCollection<NoteListItem> Notes { get; } = [];

    /// <summary>
    /// Shows what is already on the phone first, then synchronises. The other order would leave the
    /// screen blank for the length of a round trip, and empty for as long as there is no network at all.
    /// </summary>
    /// <summary>
    /// Asks the phone who is holding it, then redraws. A refusal needs no message of its own - the rows
    /// simply stay closed, which is the same thing the system's own prompt just said.
    /// </summary>
    [RelayCommand]
    private async Task UnlockPrivateAsync(CancellationToken cancellationToken)
    {
        if (await _privateItems.TryUnlockAsync(cancellationToken))
        {
            await ShowLocalNotesAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// Moves a note to the top of the list, or lets it back down. Reaches the server rather than only
    /// the phone, because the next pull overwrites what is held locally - so a pin that stayed here
    /// would quietly undo itself. Refused rather than queued when there is no connection: the outbox
    /// carries changes to a note, and this is not one - it leaves UpdatedAtUtc alone on purpose.
    /// </summary>
    [RelayCommand]
    private async Task TogglePinAsync(NoteListItem? row, CancellationToken cancellationToken)
    {
        if (row is not { CanBePinned: true }
            || await _notes.FindAsync(row.LocalId, cancellationToken) is not { ServerId: { } serverId })
        {
            return;
        }

        try
        {
            if (await _notesClient.SetPinnedAsync(serverId, !row.IsPinned, cancellationToken) is not WriteOutcome.Applied)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Pinning needs a connection."];
            return;
        }

        await _notes.MarkPinnedAsync(row.LocalId, !row.IsPinned, cancellationToken);
        Message = string.Empty;
        await ShowLocalNotesAsync(cancellationToken);
    }

    /// <summary>
    /// Opens one note. A hidden row opens nothing: it offers the lock instead, which is the whole point
    /// of hiding it - see NoteListItem.CanBeOpened.
    /// </summary>
    [RelayCommand]
    private void Open(NoteListItem? row)
    {
        if (row is { CanBeOpened: true })
        {
            _navigator.ShowNote(row.LocalId);
        }
    }

    /// <summary>The way back to the dashboard, as every other list screen has - see NotesPage.</summary>
    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    [RelayCommand(CanExecute = nameof(CanAddNote))]
    private async Task AddNoteAsync(CancellationToken cancellationToken)
    {
        await _notes.CreateAsync(NewNoteTitle.Trim(), NoteListItem.EmptyContent, cancellationToken);
        NewNoteTitle = string.Empty;

        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private bool CanAddNote => NewNoteTitle.Trim().Length > 0;

    private async Task ShowLocalNotesAsync(CancellationToken cancellationToken)
    {
        var stored = await _notes.GetAllAsync(cancellationToken);
        var pending = await _notes.GetPendingNoteLocalIdsAsync(cancellationToken);

        Notes.Clear();
        foreach (var note in stored)
        {
            Notes.Add(NoteListItem.From(
                note, pending.Contains(note.LocalId), _networkStatus, _privateItems.IsUnlocked,
                _translations, _translations["Private"]));
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
                await ShowLocalNotesAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            // The server was reached and refused - an expired session, most often. AppNavigator is
            // watching the session store and moves to sign-in when that is what happened; there is
            // nothing useful to say here beyond not claiming the phone is offline.
            _syncState.RecordFailed();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync. The command is started without being awaited, so this must
            // not escape.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// "Offline" is only said when the phone actually believes it has no connection. A sync that failed
    /// while connectivity looks fine is a different thing - a server having a bad moment - and saying
    /// "offline" would send the user looking for a network problem that isn't there.
    /// </summary>
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
    partial void OnNewNoteTitleChanged(string value) => AddNoteCommand.NotifyCanExecuteChanged();
}
