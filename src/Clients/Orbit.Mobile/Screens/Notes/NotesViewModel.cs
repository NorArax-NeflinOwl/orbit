using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sync;
using Orbit.Core.Folders;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Folders;
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
    private readonly IListArrangementStore _arrangements;
    private readonly LocalFolderRepository _folders;

    /// <summary>
    /// Sends the folders themselves. Its own run rather than part of the note sync, and before it: a
    /// note filed into a folder made a moment ago names a folder the server has not been told about,
    /// and nothing can be filed into one of those - see FolderNotOnTheServerYet.
    /// </summary>
    private readonly FolderSynchronizer _folderSynchronizer;

    /// <summary>What "today" is, so a card's footnote says it against the clock the tests hand over.</summary>
    private readonly TimeProvider _clock;


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
        SyncState syncState, IScreenNavigator navigator, TimeProvider clock,
        IListArrangementStore arrangements, LocalFolderRepository folders, IChosenFolderStore chosenFolder,
        FolderSynchronizer folderSynchronizer)
    {
        _folderSynchronizer = folderSynchronizer;
        Folders = new FolderTabs(folders, chosenFolder, translations, FolderPage.Notes);
        _clock = clock;
        _folders = folders;
        _arrangements = arrangements;
        _arrangement = arrangements.Read(ListSection.Notes);
        _notes = notes;
        _synchronizer = synchronizer;
        _notesClient = notesClient;
        _networkStatus = networkStatus;
        _translations = translations;
        _privateItems = privateItems;
        _syncState = syncState;
        _navigator = navigator;
        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNotes));
    }

    public ObservableCollection<NoteListItem> Notes { get; } = [];

    /// <summary>
    /// The folders this screen offers and which of them is being read - see FolderTabs. The browser
    /// draws them as a row of tabs above the cards; here they are entries in the menu under the
    /// screen's name, which is what the design draws.
    /// </summary>
    public FolderTabs Folders { get; }

    /// <summary>Those folders as the menu draws them, with how many notes are in each.</summary>
    public ObservableCollection<FolderChoice> FolderChoices { get; } = [];

    /// <summary>What the screen is narrowed to, for the empty list to say so rather than look broken.</summary>
    public string ChosenFolderName
        => FolderChoices.FirstOrDefault(choice => choice.IsChosen)?.Name ?? string.Empty;

    /// <summary>
    /// Whether the list has anything in it. The screen draws a hairline above every row and one more
    /// below the last, so that the column closes rather than stopping mid-air - and that closing line
    /// is the one thing that must not be drawn under an empty list, where it would be a rule under the
    /// words "No notes."
    /// </summary>
    public bool HasNotes => Notes.Count > 0;

    /// <inheritdoc cref="Tasks.TasksViewModel.HasMessage"/>
    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

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

    /// <summary>
    /// Taking a note off the list, which the card's own menu offers - see NotesPage, and Orbit.Web's
    /// Notes card, which offers exactly this. A note somebody else owns is not this reader's to
    /// delete: the same press takes it off their own list and leaves the owner's alone, which is why
    /// the card names it differently for a shared one.
    ///
    /// Asking first is the page's job, not this one's: what a question looks like is a screen's
    /// business, and there is nothing here to ask with.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(NoteListItem? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var deletion = await _notes.DeleteAsync(row.LocalId, cancellationToken);
        if (deletion.WasRefused())
        {
            Message = deletion.Explain(RefusalMessage, _translations);
            return;
        }

        Message = string.Empty;
        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>The way back to the dashboard, as every other list screen has - see NotesPage.</summary>

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
        await Folders.ReadAsync(cancellationToken);

        // Where each note is, by the rule both clients share - a note has nothing to finish, so the
        // question a task list is asked here is not asked of it. See FolderPlacement.
        var placements = stored.ToDictionary(
            note => note.LocalId,
            note => Folders.Where(note.FolderId, note.IsPrivate, isFinished: false));

        FolderChoices.Clear();
        foreach (var choice in Folders.Describe(placements.Values))
        {
            FolderChoices.Add(choice);
        }

        OnPropertyChanged(nameof(ChosenFolderName));

        var rows = stored
            .Where(note => Folders.Holds(placements[note.LocalId]))
            .Select(note => NoteListItem.From(
                note, pending.Contains(note.LocalId), _networkStatus, _privateItems.IsUnlocked,
                _translations, _clock.GetUtcNow(), _translations["Private"]));

        Notes.Clear();
        foreach (var row in ListArrangements.Apply(rows, Arrangement, Describe))
        {
            Notes.Add(row);
        }
    }

    /// <summary>Reading the screen under another folder - chosen from the menu under its name.</summary>
    [RelayCommand]
    private async Task ChooseFolderAsync(FolderKey key, CancellationToken cancellationToken)
    {
        Folders.Choose(key);
        await ShowLocalNotesAsync(cancellationToken);
    }

    /// <summary>
    /// Puts one note in a folder, or takes it out of one. Refused offline for a note somebody else can
    /// change, exactly as an edit is - filing is a decision about the owner's own page, and the same
    /// rule that stops an edit being lost stops this one - see LocalNoteRepository.FileAsync.
    /// </summary>
    [RelayCommand]
    private async Task FileAsync((NoteListItem Note, Guid? FolderId) filing, CancellationToken cancellationToken)
    {
        var outcome = await _notes.FileAsync(filing.Note.LocalId, filing.FolderId, cancellationToken);

        Message = outcome is LocalWriteOutcome.RefusedWhileOffline
            ? _translations["This one can't be moved while you're offline."]
            : string.Empty;

        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// A new folder, made and shown at once whether or not there is a connection - a folder is a name,
    /// so nothing about it waits on a server, and a reader who cannot make a tab until they have signal
    /// cannot tidy up on a train. The screen moves to it, because making one is how somebody says where
    /// the next thing goes.
    /// </summary>
    [RelayCommand]
    private async Task MakeFolderAsync(string? name, CancellationToken cancellationToken)
    {
        if (name?.Trim() is not { Length: > 0 } wanted)
        {
            return;
        }

        if (FolderBeingRenamed is { } renamed)
        {
            // The same row, answering the other question - see FolderBeingRenamed. The screen stays
            // where it is: the folder being read has a new name, not a new place.
            await _folders.RenameAsync(renamed, wanted, cancellationToken);
            FolderBeingRenamed = null;
        }
        else
        {
            var folder = await _folders.CreateAsync(wanted, FolderScope.Notes, cancellationToken);
            Folders.Choose(FolderKey.Of(folder.LocalId));
        }

        NewFolderName = string.Empty;
        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>The name being typed into the folder row - see NotesPage, which unfolds it.</summary>
    [ObservableProperty]
    private string _newFolderName = string.Empty;

    /// <summary>
    /// The folder whose name the row is changing, or null while the row names a new one. One row for
    /// both questions rather than a dialog for the second: a folder is a name, and Android's own prompt
    /// would sit badly beside Orbit's panel. What the row's button says follows it.
    /// </summary>
    [ObservableProperty]
    private Guid? _folderBeingRenamed;

    /// <summary>What the folder row's button says - Add for a new folder, Rename for one being renamed.</summary>
    public string FolderRowAction => FolderBeingRenamed is null ? _translations["Add"] : _translations["Rename"];

    partial void OnFolderBeingRenamedChanged(Guid? value) => OnPropertyChanged(nameof(FolderRowAction));

    /// <summary>Puts the open folder's name in the row so it can be changed - see NotesPage.FolderActions.</summary>
    public void StartRenamingTheOpenFolder()
    {
        if (Folders.Chosen.FolderId is { } folderId)
        {
            FolderBeingRenamed = folderId;
            NewFolderName = ChosenFolderName;
        }
    }

    /// <summary>Empties the row for a new folder, whatever it was doing before.</summary>
    public void StartNamingANewFolder()
    {
        FolderBeingRenamed = null;
        NewFolderName = string.Empty;
    }

    /// <summary>
    /// Takes the folder being read away and leaves everything that was in it, which is what the server
    /// does too: a folder is a place to put things, and getting rid of the place is not a decision to
    /// get rid of them. They fall back to a built-in folder on their own.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(CancellationToken cancellationToken)
    {
        if (Folders.Chosen.FolderId is not { } folderId)
        {
            return;
        }

        await _folders.DeleteAsync(folderId, cancellationToken);
        Folders.Choose(FolderKey.Default);

        await ShowLocalNotesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }


    /// <summary>
    /// How this screen is being read - what order, and what it is narrowed to. Held on the device, so
    /// coming back to the notes finds them the way they were left. See <see cref="ListArrangement"/>.
    /// </summary>
    [ObservableProperty]
    private ListArrangement _arrangement;

    /// <summary>
    /// Chosen from the menu under the screen's name - see NotesPage. Written down as it is chosen, the
    /// way every other setting on the phone is.
    /// </summary>
    [RelayCommand]
    private async Task ArrangeAsync(ListArrangement? arrangement, CancellationToken cancellationToken)
    {
        if (arrangement is null || arrangement == Arrangement)
        {
            return;
        }

        Arrangement = arrangement;
        _arrangements.Write(ListSection.Notes, arrangement);
        await ShowLocalNotesAsync(cancellationToken);
    }

    /// <summary>What the shared ordering needs to know about one row - see ListArrangements.</summary>
    private static ListRowFacts Describe(NoteListItem row)
        => new(row.DisplayTitle, row.UpdatedAtUtc, row.IsPinned, row.PriorityValue);

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            // The folders first, and always - see _folderSynchronizer. This screen shows them, so it
            // keeps them current the same way it keeps the notes current; and a folder made here with no
            // connection would otherwise sit in the queue until somebody happened to make another one.
            await _folderSynchronizer.SynchroniseAsync(cancellationToken);

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

    /// <summary>
    /// The dictionary key, not the text itself - see <see cref="Translations"/>. The same sentence the
    /// note's own screen uses, because it is the same refusal about the same note.
    /// </summary>
    private const string RefusalMessage =
        "Somebody else can change this note, and Orbit can't be reached to check. It stays read-only until you're back online.";
}
