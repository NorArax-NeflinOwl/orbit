using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notes;
using Orbit.Core.Folders;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One note and what it says. The counterpart of <see cref="Tasks.TaskListDetailViewModel"/> and shaped
/// the same way: every change is written to the local database first and queued from there, so writing
/// works with no connection and the screen never waits on a request.
///
/// <b>A private note is opened here rather than only carried through.</b> Its words live inside a
/// payload sealed under the account's own key (see PrivateContentSealer), which this phone holds for
/// chat already - so the same note reads the same in a browser and here, and the checkbox that makes
/// one is on this screen exactly as it is in Orbit.Web's editor. A note this device cannot open - no
/// key, or a key pair since replaced - still opens read-only and says which of those it is.
/// </summary>
public sealed partial class NoteDetailViewModel : ObservableObject
{
    private readonly LocalNoteRepository _notes;
    private readonly LocalFolderRepository _folders;
    private readonly NoteSynchronizer _synchronizer;
    private readonly NotesClient _notesClient;
    private readonly EditLock _editLock;
    private readonly Translations _translations;
    private readonly PrivateContentSealer _privateContent;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _newLine = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>Why it cannot be changed, when it cannot - a sealed note, or somebody else's while offline.</summary>
    [ObservableProperty]
    private string _readOnlyReason = string.Empty;

    /// <summary>
    /// Whether to ask "then take a copy?" - offered only where the refusal is one a copy answers: no
    /// connection, and somebody else able to change this. A sealed note is not offered one (there is
    /// nothing readable to copy), and neither is a note somebody is in right now, because that refusal
    /// ends by itself in a minute and a copy would outlive it.
    /// </summary>
    [ObservableProperty]
    private bool _isCopyOffered;

    /// <summary>
    /// Only its owner may ever read this note, and the server never can. Orbit.Web's editor has had the
    /// checkbox all along; the phone honoured the flag - hiding such a note behind the device lock - but
    /// could not set one, so a note made here could never be private.
    /// </summary>
    [ObservableProperty]
    private bool _isPrivate;

    /// <summary>
    /// Somebody else's note, on this reader's list. What "delete" means changes with it: theirs is not
    /// this reader's to destroy, so the same press takes it off their own list and leaves the owner's
    /// alone - which is what the server does, and what the menu should therefore say.
    /// </summary>
    [ObservableProperty]
    private bool _isSharedWithMe;

    public NoteDetailViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, NotesClient notesClient, EditLock editLock,
        Translations translations, PrivateContentSealer privateContent, SharePanel share, IScreenNavigator navigator,
        LocalFolderRepository folders)
    {
        _folders = folders;
        _notes = notes;
        _synchronizer = synchronizer;
        _notesClient = notesClient;
        _editLock = editLock;
        _translations = translations;
        _privateContent = privateContent;
        Share = share;
        _navigator = navigator;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();

        Priorities = Tasks.PriorityChoice.All(translations);
        _chosenPriority = Tasks.PriorityChoice.For(nameof(Orbit.Core.Abstractions.ItemPriority.Normal), translations);
    }

    /// <summary>
    /// How much this note matters - Orbit.Web's note editor has had the same three choices all along,
    /// and the phone's own dashboard filters by them without ever being able to set one.
    /// </summary>
    public IReadOnlyList<Tasks.PriorityChoice> Priorities { get; }

    /// <summary>
    /// The folders this note could be filed under - the ones somebody made on the notes screen. Offered
    /// here rather than from the list, because everything that can be done to a note is under its own
    /// name once it is open: the list gave up its per-row menu for that.
    /// </summary>
    public IReadOnlyList<LocalFolder> Folders { get; private set; } = [];

    /// <summary>Which of them it is in, or null for one in none - see FolderPlacement.</summary>
    [ObservableProperty]
    private Guid? _folderId;

    /// <summary>
    /// Puts it in a folder, or takes it out of one. Written down at once and queued behind whatever
    /// else is waiting, like every other change made here - see LocalNoteRepository.FileAsync, which
    /// explains why filing is its own kind of change rather than part of the save.
    /// </summary>
    [RelayCommand]
    private async Task FileAsync(Guid? folderId, CancellationToken cancellationToken)
    {
        var outcome = await _notes.FileAsync(_localId, folderId, cancellationToken);

        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations["This one can't be moved while you're offline."];
            return;
        }

        FolderId = folderId;
        Status = string.Empty;
    }

    [ObservableProperty]
    private Tasks.PriorityChoice _chosenPriority;

    /// <inheritdoc cref="Tasks.TaskListDetailViewModel"/>
    private string _priority = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    public ObservableCollection<NoteLineRow> Lines { get; } = [];

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredNoteAsync(cancellationToken);

    /// <summary>
    /// Writes down what the note says now. The one thing the button over the foot of the screen does -
    /// the design gives this editor a Save, where every other screen in Orbit writes as it goes, because
    /// this one is a page of prose being typed rather than a form of answers being chosen.
    ///
    /// Saving does not redraw the note from the store. It used to, and doing that here would take the
    /// caret out of whatever line was being written in the moment it was written.
    /// </summary>
    [RelayCommand]
    private Task SaveLinesAsync(CancellationToken cancellationToken)
        => CanEdit ? WriteAsync(cancellationToken) : Task.CompletedTask;

    /// <summary>
    /// A new line under the one being written in, which is what Enter does on a surface like this.
    ///
    /// <paramref name="caret"/> is where in that line the press happened, and **whatever follows it
    /// moves down onto the new line** - Enter in the middle of a sentence breaks the sentence, which is
    /// what it does in every text field there is. Pass the length of the line (or leave it out) for a
    /// press at the end, where there is nothing to carry down.
    ///
    /// The new line inherits the indentation of the line above - a list stays a list when a line is
    /// added to the middle of it, which is what an editor doing anything else gets wrong first. It is
    /// tickable when the checklist button is on, and also when it is carrying the tail of a line that
    /// was itself tickable: a checklist continues as a checklist until a line is left empty.
    /// </summary>
    public NoteLineRow AddLineAfter(NoteLineRow? row, int caret = int.MaxValue)
    {
        var above = row ?? Lines.LastOrDefault();
        var fresh = new NoteLineRow();

        if (above is not null)
        {
            // Read before the line is cut: a press at the very start leaves nothing above to take the
            // indentation from, and the new line is still the same line's continuation.
            var indentation = IndentationOf(above.Text);
            var at = Math.Clamp(caret, 0, above.Text.Length);
            var carried = above.Text[at..];

            above.Text = above.Text[..at];
            fresh.Text = indentation + carried;

            // A checklist goes on being a checklist - but an empty line ends it, which is how a reader
            // stops one without reaching for the button in the corner.
            fresh.IsChecklistItem = IsWritingAChecklist
                || (above.IsChecklistItem && (above.Text.Length > 0 || carried.Length > 0));
        }
        else
        {
            fresh.IsChecklistItem = IsWritingAChecklist;
        }

        Lines.Insert(above is null ? Lines.Count : Lines.IndexOf(above) + 1, fresh);
        Watch(fresh);
        return fresh;
    }

    /// <summary>
    /// The leading whitespace of a line, which the next one starts with. Tabs and spaces both: a note
    /// written on a keyboard indents with one, a note written in a browser with the other, and the
    /// editor should not have an opinion about which of them counts.
    /// </summary>
    private static string IndentationOf(string text)
        => text[..(text.Length - text.TrimStart('\t', ' ').Length)];

    /// <summary>
    /// Backspace at the very start of a line: the line joins the one above it, exactly as it would in
    /// any text field, and the caret lands where the two meet. Returns where that is, or null when the
    /// press means nothing - or when it meant something other than a merge, which is the tick box.
    ///
    /// **A line with a tick box loses the box first.** Backspace at the head of one takes it off and
    /// leaves the words where they are; only a second press joins what is left to the line above. It is
    /// the one way to undo a box from the keyboard, and it stops a reader who typed "[]" by accident
    /// from having to reach for the button in the corner to undo it - which is what the design does.
    /// </summary>
    public (NoteLineRow Line, int Caret)? MergeIntoTheLineAbove(NoteLineRow? row)
    {
        if (row is null || Lines.IndexOf(row) is var index && index < 0)
        {
            return null;
        }

        if (row.IsChecklistItem)
        {
            row.IsChecklistItem = false;
            row.IsChecked = false;
            return null;
        }

        if (index == 0)
        {
            return null;
        }

        var above = Lines[index - 1];
        var caret = above.Text.Length;
        above.Text += row.Text;
        Lines.RemoveAt(index);
        return (above, caret);
    }

    /// <summary>
    /// Where the caret goes when the reader presses arrow up in <paramref name="row"/>: the line over
    /// it, or null when there is none - the writing starts at the note's name, and the page takes the
    /// caret there rather than leaving the press to do nothing.
    ///
    /// The editor is a column of one-line fields, so a line never wraps and an arrow always means the
    /// line beside this one rather than the row above the caret inside it. Left to Android, the key
    /// stopped at the ends of the field it was in, so the only way from one line to the next was to reach
    /// up and press it.
    /// </summary>
    public NoteLineRow? TheLineAbove(NoteLineRow? row)
    {
        var index = row is null ? -1 : Lines.IndexOf(row);
        return index <= 0 ? null : Lines[index - 1];
    }

    /// <summary>
    /// The same downwards, for arrow down. Null at the last line: the arrow walks the writing that is
    /// there and does not start a line, which is Enter's job and nothing else's.
    /// </summary>
    public NoteLineRow? TheLineBelow(NoteLineRow? row)
    {
        var index = row is null ? -1 : Lines.IndexOf(row);
        return index < 0 || index >= Lines.Count - 1 ? null : Lines[index + 1];
    }

    /// <summary>
    /// Whether the next line started will be a tickable one. The button in the bottom-left corner of
    /// the editor turns this on and puts a box on the line being written in; pressing it again takes
    /// that box off again and turns it back off - which is what the design asks of one control.
    /// </summary>
    [ObservableProperty]
    private bool _isWritingAChecklist;

    /// <summary>Turns a line into a checklist item, or back into an ordinary one.</summary>
    [RelayCommand]
    private void ToggleChecklist(NoteLineRow? row)
    {
        if (row is null || IsReadOnly)
        {
            return;
        }

        row.IsChecklistItem = !row.IsChecklistItem;
        row.IsChecked = false;
        IsWritingAChecklist = row.IsChecklistItem;
    }

    /// <summary>
    /// Moves a line to the next of the three answers - nothing, done, given up on - which is the same
    /// cycle the browser's own box follows. See <see cref="NoteLineRow.Press"/> and TickState.
    ///
    /// Ticked in place and **not** written down: a tick is a change to the note like any other on this
    /// screen, and the note is written by Save and by nothing else - see <see cref="CloseAsync"/>. It
    /// used to write immediately, which meant a tick survived leaving the screen while the words typed
    /// beside it did not.
    /// </summary>
    [RelayCommand]
    private void ToggleChecked(NoteLineRow? row)
    {
        if (row is not { IsChecklistItem: true } || IsReadOnly)
        {
            return;
        }

        row.Press();
    }

    /// <summary>Renaming saves the whole note, because the API's update takes the whole note.</summary>
    [RelayCommand]
    private Task RenameAsync(CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var deletion = await _notes.DeleteAsync(_localId, cancellationToken);
        if (deletion.WasRefused())
        {
            Status = deletion.Explain(RefusalMessage, _translations);
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowNotes();
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    /// <summary>
    /// Takes the copy the reader has just asked for and opens it, so they carry on writing where they
    /// meant to rather than being returned to the list to find it.
    /// </summary>
    [RelayCommand]
    private async Task CopyForEditingAsync(CancellationToken cancellationToken)
    {
        if (await _notes.CopyForEditingAsync(_localId, cancellationToken) is not { } copy)
        {
            return;
        }

        IsCopyOffered = false;
        _navigator.ShowNote(copy.LocalId);
    }

    /// <summary>Reading it and leaving it alone, which is the ordinary answer - and asked only once.</summary>
    [RelayCommand]
    private void DeclineCopy() => IsCopyOffered = false;

    /// <summary>
    /// Writes the note down as the screen has it, and says whether it went. Nothing is read back, so
    /// whatever line is being written in stays where it is with the caret in it - which is why this and
    /// <see cref="SaveAsync"/> are two methods rather than one with a flag.
    /// </summary>
    private async Task<bool> WriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _notes.UpdateAsync(
                _localId,
                new NoteContent(Title.Trim(), [.. Lines.Select(line => line.ToDto())], _priority, IsPrivate),
                cancellationToken);
            if (outcome.WasRefused())
            {
                Status = outcome.Explain(RefusalMessage, _translations);
                return false;
            }
        }
        catch (EncryptionKeyLockedException)
        {
            // Sealing needs the account's own key, and this device has not got it. The key gate is where
            // that is fixed, and it is where chat sends people for the same reason.
            _navigator.ShowChatKeyGate();
            return false;
        }

        // Written down, so leaving no longer has anything to ask about.
        RememberWhatIsWrittenDown();

        await SynchroniseAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Writes the note down and then reads it back, which is what a change to what the note *is* -
    /// its name, how much it matters, whether it is private - needs: the answer decides whether it can
    /// still be shared and whether it can still be edited at all.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (await WriteAsync(cancellationToken))
        {
            await ShowStoredNoteAsync(cancellationToken);
        }
    }

    private async Task ShowStoredNoteAsync(CancellationToken cancellationToken)
    {
        if (await _notes.FindAsync(_localId, cancellationToken) is not { } note)
        {
            _navigator.ShowNotes();
            return;
        }

        Title = note.Title;
        IsSharedWithMe = note.IsShared;
        FolderId = note.FolderId;
        Folders = [.. (await _folders.GetAllAsync(FolderScope.Notes, cancellationToken))];
        _isShowingWhatIsStored = true;
        ChosenPriority = Tasks.PriorityChoice.For(note.Priority, _translations);
        IsPrivate = note.IsPrivate;
        _isShowingWhatIsStored = false;

        // Only a note the server knows about can be offered: a share names it by its server id, and one
        // still waiting in the outbox has none. A private note is offered to nobody - the server holds
        // no readable copy to hand over, which is what makes it private.
        if (note is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(SharedItemKind.Note, serverId, note.Title, OwnerToAsk(note));
        }
        else
        {
            Share.OffersNothing();
        }

        HasHistory = (await _notes.GetHistoryOfAsync(_localId, cancellationToken)).Count > 0;
        await ShowWhetherItCanBeChangedAsync(note, cancellationToken);

        ShowTheLines(note.Content);
    }

    /// <summary>
    /// Puts the stored lines on the screen and starts listening to each of them - see
    /// <see cref="Watch"/>, which is what turns a typed "[]" into a real tick box.
    /// </summary>
    private void ShowTheLines(IReadOnlyList<NoteContentLineDto> content)
    {
        foreach (var line in Lines)
        {
            line.PropertyChanged -= WhenALineChanges;
        }

        Lines.Clear();
        foreach (var line in content)
        {
            var row = NoteLineRow.From(line);
            Lines.Add(row);
            Watch(row);
        }

        // A note with nothing in it still needs somewhere to put the caret. The store keeps one empty
        // line for exactly this - see NoteListItem.EmptyContent - but a note whose last line was
        // deleted has none, and a surface with no lines at all cannot be written in.
        if (Lines.Count == 0)
        {
            AddLineAfter(null);
        }

        RememberWhatIsWrittenDown();
    }

    /// <summary>
    /// What the note said the last time it was read or written, so that leaving can tell an edit from a
    /// note somebody only looked at - see <see cref="HasUnsavedChanges"/>.
    /// </summary>
    private string _writtenDown = string.Empty;

    /// <summary>
    /// Everything a Save would send, as one string. Compared rather than tracked with a flag: a flag
    /// says "something was touched", and something touched and put back is not a change - typing a
    /// letter and deleting it would leave a note asking to be saved with nothing to save.
    /// </summary>
    private string WhatIsOnTheScreen()
        => string.Join(
            '\u001f',
            Lines
                .Select(line => $"{line.Text}\u001e{line.IsChecklistItem}\u001e{line.IsChecked}")
                .Prepend(Title));

    private void RememberWhatIsWrittenDown() => _writtenDown = WhatIsOnTheScreen();

    /// <summary>
    /// Whether leaving now would lose something. False on a note nobody can edit, and false once Save
    /// has been pressed - which is the whole of what the question at the door needs to know.
    /// </summary>
    public bool HasUnsavedChanges => CanEdit && WhatIsOnTheScreen() != _writtenDown;

    /// <summary>
    /// Watches one line for the mark that makes it tickable.
    ///
    /// The design gives this editor no toolbar to speak of: the reader types "[]" where they want a box
    /// and gets one. The mark is taken back out of the text as it is recognised, because what it means
    /// is now carried by the line itself - see NoteContentLineDto, which is the same shape Orbit.Web's
    /// editor writes and reads.
    /// </summary>
    private void Watch(NoteLineRow row) => row.PropertyChanged += WhenALineChanges;

    private void WhenALineChanges(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(NoteLineRow.Text) || sender is not NoteLineRow row)
        {
            return;
        }

        var indentation = IndentationOf(row.Text);
        var rest = row.Text[indentation.Length..];

        foreach (var mark in ChecklistMarks)
        {
            if (!rest.StartsWith(mark, StringComparison.Ordinal))
            {
                continue;
            }

            row.IsChecklistItem = true;
            // TrimStart of one space only: "[] " and "[]" both mean the same thing, and anything the
            // reader typed beyond that is theirs.
            var written = rest[mark.Length..];
            row.Text = indentation + (written.StartsWith(' ') ? written[1..] : written);
            IsWritingAChecklist = true;
            return;
        }
    }

    /// <summary>
    /// What the reader can type to ask for a tick box. Both spellings, because a phone keyboard puts a
    /// space inside the brackets as readily as not.
    /// </summary>
    private static readonly string[] ChecklistMarks = ["[]", "[ ]"];

    private async Task ShowWhetherItCanBeChangedAsync(LocalNote note, CancellationToken cancellationToken)
    {
        if (note.IsSealed)
        {
            // Nothing to show and nothing that could be sent back: saving would replace a sealed note
            // with an empty one. Which of the two reasons it is decides what the reader can do about it.
            IsReadOnly = true;
            ReadOnlyReason = await _privateContent.HasKeyAsync(cancellationToken)
                ? _translations["This note was sealed with an encryption key this account no longer has."]
                : _translations["This note is private. Unlock this device's encryption key to read it."];
            return;
        }

        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        IsReadOnly = !await _notes.CanEditAsync(_localId, cancellationToken);
        // Which of the two reasons it is decides what the reader can do about it: waiting for a
        // connection helps with one and never with the other - see SharedItemAccess.
        var sharedToRead = SharedItemAccess.WhyItCannotBeEdited(note, _translations);
        ReadOnlyReason = string.Empty;
        if (IsReadOnly)
        {
            ReadOnlyReason = sharedToRead.Length > 0 ? sharedToRead : _translations[RefusalMessage];
        }
        // A copy is for editing offline what could be edited online, so there is nothing to take one of
        // when the share itself does not permit editing.
        IsCopyOffered = IsReadOnly && note.CopyOfLocalId is null && SharedItemAccess.AllowsEditing(note);

        if (IsReadOnly || note.ServerId is not { } serverId)
        {
            return;
        }

        // Claimed for as long as this screen is open, so somebody editing the same note on the web is
        // told rather than left to have their save refused - see EditLock.
        await _editLock.HoldAsync(_notesClient, serverId, cancellationToken);
        ShowWhoElseIsEditing();
    }

    private void ShowWhoElseIsEditing()
    {
        if (!_editLock.IsHeldByAnother)
        {
            return;
        }

        IsReadOnly = true;
        ReadOnlyReason = _editLock.RefusalMessage;
    }

    /// <summary>
    /// Lets the note go when the screen does, rather than leaving it claimed for a minute.
    ///
    /// It does **not** write anything down. Leaving used to save whatever had been typed, which made the
    /// button in the corner mean nothing: the note was already written by the time anybody could press
    /// it, and there was no way to try a change and then decide against it. The writing is committed by
    /// Save and by nothing else, so leaving is how a reader abandons an edit. Every other detail screen
    /// in the app already closes this way - see TaskListDetailViewModel, CalendarEventDetailViewModel
    /// and InventoryDetailViewModel, whose CloseAsync releases the lock and stops.
    /// </summary>
    public Task CloseAsync() => _editLock.ReleaseAsync();

    /// <summary>
    /// Pushes what was just queued, and says so if it could not go. Nothing is lost either way - the
    /// change is already in the outbox - so this is about telling the reader, not about the write.
    /// </summary>
    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer
                ? string.Empty
                : _translations["Saved on this phone - it will sync later"];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    /// <summary>
    /// Whoever to ask for more access, or null when there is nobody to ask: your own note, or one you
    /// can already change. Asking about either would be asking for what you have.
    /// </summary>
    private static Guid? OwnerToAsk(LocalNote note)
        => note.AccessLevel == "CanEdit" ? null : note.OwnerUserId;

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this note, and Orbit can't be reached to check. It stays read-only until you're back online.";

    /// <summary>
    /// Saved as soon as it is chosen, the way ticking a line is. The value comes from here rather than
    /// off the property: a save started from this hook must not have to guess whether the property has
    /// caught up - see TaskListDetailViewModel, where it had not.
    /// </summary>
    partial void OnChosenPriorityChanged(Tasks.PriorityChoice value)
    {
        _priority = value.Value;
        if (!_isShowingWhatIsStored && CanEdit)
        {
            SaveAndReadBackCommand.Execute(null);
        }
    }

    /// <inheritdoc cref="OnChosenPriorityChanged"/>
    partial void OnIsPrivateChanged(bool value)
    {
        if (!_isShowingWhatIsStored && CanEdit)
        {
            SaveAndReadBackCommand.Execute(null);
        }
    }

    /// <summary>
    /// What a change to the note itself does - see <see cref="SaveAsync"/>. Sealing a note takes it out
    /// of everybody else's reach, so what the screen may still offer has to be asked again.
    /// </summary>
    [RelayCommand]
    private Task SaveAndReadBackAsync(CancellationToken cancellationToken) => SaveAsync(cancellationToken);

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    /// <summary>
    /// Whether anything was ever copied from this - what puts its history within reach. Hidden until
    /// there is one, because most things have none and a permanent link to an empty window is clutter.
    /// </summary>
    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>This thing's own history, opened from this thing - see CopyHistoryViewModel.</summary>
    [RelayCommand]
    private void GoToHistory() => _navigator.ShowCopyHistory(CopyKind.Note, _localId);
}
