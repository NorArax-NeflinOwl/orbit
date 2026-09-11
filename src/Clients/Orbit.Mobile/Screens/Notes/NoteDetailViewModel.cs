using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notes;
using Orbit.Core.Folders;
using Orbit.Core.Notes;
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
    private readonly TimeProvider _timeProvider;

    private Guid _localId;

    /// <summary>
    /// Undo and redo for the writing - the same history Orbit.Web's editor keeps (see
    /// Orbit.Core.Notes.NoteSurfaceHistory), of whole states of the note with the caret in them. The
    /// note's name is the surface's first line, as it is in the browser, so an undo reaches it too.
    ///
    /// A phone has no Ctrl+Z, so the two are buttons beside the tick-box button over the note's foot.
    /// </summary>
    private readonly NoteSurfaceHistory _history =
        new(SurfaceState.CaretAt([SurfaceState.EmptyLine, SurfaceState.EmptyLine], new SurfacePoint(0, 0)));

    /// <summary>Which note <see cref="_history"/> is the history of, so another note opened here starts its own.</summary>
    private Guid _historyOf;

    /// <summary>
    /// Above zero while this view model is changing the lines itself - an undo, a line split by Enter, a
    /// note being read in. A line's text changing then is not somebody typing, and neither the history
    /// nor the reading of a typed "[]" may take it for that.
    /// </summary>
    private int _applying;

    /// <summary>
    /// Says where the caret belongs after an edit the screen made rather than the keyboard - see
    /// <see cref="NoteCaret"/>. Raised for an undo or redo that changed words (one that only put a tick
    /// back leaves the caret alone, as pressing the box did) and after a typed "[]" is taken out of a
    /// line, whose field is rewritten under the caret.
    /// </summary>
    public event EventHandler<NoteCaret>? CaretPlaced;

    /// <summary>
    /// The left half of the editor's foot, as the design draws it: whose note this is when it is not the
    /// reader's own, and when it last changed - in the words the note's card on the list uses, so the two
    /// say the same thing (see LastChanged). Somebody the reader shared it *with* is not named: this
    /// phone keeps only that it is shared, not with whom.
    /// </summary>
    [ObservableProperty]
    private string _footnote = string.Empty;

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

    /// <summary>
    /// What the note is tagged with, and the colours of those tags - see TagsForm. Saved with the note; a
    /// colour is saved there and then, for the whole account.
    /// </summary>
    public Orbit.Mobile.Screens.Tags.TagsForm Tags { get; }

    public NoteDetailViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, NotesClient notesClient, EditLock editLock,
        Translations translations, PrivateContentSealer privateContent, SharePanel share, IScreenNavigator navigator,
        LocalFolderRepository folders, TimeProvider timeProvider,
        LocalTagColourRepository? tagColours = null, TagColourSynchronizer? tagColourSynchronizer = null)
    {
        Tags = new Orbit.Mobile.Screens.Tags.TagsForm(translations, tagColours, tagColourSynchronizer);
        _timeProvider = timeProvider;
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
        // A chosen line that goes - joined, undone - is no longer chosen, and the count has to say so.
        Lines.CollectionChanged += (_, _) => SayWhatIsPicked();

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
        var pressedAt = above is null
            ? new SurfacePoint(0, Title.Length)
            : new SurfacePoint(Lines.IndexOf(above) + 1, Math.Clamp(caret, 0, above.Text.Length));

        Edit(SurfaceEditKind.Reshaping, pressedAt, () =>
        {
            if (above is not null)
            {
                // Read before the line is cut: a press at the very start leaves nothing above to take the
                // indentation from, and the new line is still the same line's continuation.
                var indentation = IndentationOf(above.Text);
                var at = pressedAt.Offset;
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
            return new SurfacePoint(Lines.IndexOf(fresh) + 1, IndentationOf(fresh.Text).Length);
        });

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

        var head = new SurfacePoint(index + 1, 0);

        if (row.IsChecklistItem)
        {
            Edit(SurfaceEditKind.Reshaping, head, () =>
            {
                row.IsChecklistItem = false;
                row.IsChecked = false;
                return head;
            });
            return null;
        }

        if (index == 0)
        {
            return null;
        }

        var above = Lines[index - 1];
        var caret = above.Text.Length;
        Edit(SurfaceEditKind.Reshaping, head, () =>
        {
            above.Text += row.Text;
            row.PropertyChanged -= WhenALineChanges;
            Lines.RemoveAt(index);
            return new SurfacePoint(index, caret);
        });
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

        var at = new SurfacePoint(Lines.IndexOf(row) + 1, row.Text.Length);
        Edit(SurfaceEditKind.Reshaping, at, () =>
        {
            row.IsChecklistItem = !row.IsChecklistItem;
            row.IsChecked = false;
            return at;
        });
        IsWritingAChecklist = row.IsChecklistItem;
    }

    /// <summary>
    /// Moves a line to the next of the three answers - nothing, done, given up on - which is the same
    /// cycle the browser's own box follows - Orbit.Core's NoteSurfaceEdits.Cycle, which both clients
    /// press a box with. See TickState.
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

        // While boxes are being chosen, a press on one of two or more chosen boxes answers for all of
        // them, each taking the pressed box's next answer - Orbit.Core's rule, the browser's too. Any
        // other press answers only for its own box.
        var pressed = Lines.IndexOf(row);
        IReadOnlyList<int> together = IsPickingLines ? PickedLines() : [];

        // A step of its own in the history, and one that does not move the caret: pressing a box is not
        // writing, so undoing it leaves the caret where the reader has it.
        var caret = _history.Current.Caret;
        Edit(SurfaceEditKind.Ticking, caret, () =>
        {
            if (NoteSurfaceEdits.Cycle([.. Lines.Select(line => line.ToLine())], pressed, together) is { } ticked)
            {
                for (var index = 0; index < ticked.Count; index++)
                {
                    Lines[index].IsChecked = ticked[index].IsChecked;
                    Lines[index].IsFailed = ticked[index].IsFailed;
                }
            }

            return caret;
        });
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
                new NoteContent(Title.Trim(), [.. Lines.Select(line => line.ToDto())], _priority, IsPrivate, Tags.ToSave),
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

        // Read in, not typed - see _applying.
        _applying++;
        try
        {
            Title = note.Title;
        }
        finally
        {
            _applying--;
        }

        IsSharedWithMe = note.IsShared;
        var lastChanged = LastChanged.Describe(note.UpdatedAtUtc, _timeProvider.GetUtcNow(), _translations);
        Footnote = note is { IsShared: true, SharedByUserName: { Length: > 0 } sharedBy }
            ? _translations.Format("Shared by {0} · {1}", sharedBy, lastChanged)
            : lastChanged;
        FolderId = note.FolderId;
        Folders = [.. (await _folders.GetAllAsync(FolderScope.Notes, cancellationToken))];
        _isShowingWhatIsStored = true;
        ChosenPriority = Tasks.PriorityChoice.For(note.Priority, _translations);
        IsPrivate = note.IsPrivate;
        _isShowingWhatIsStored = false;
        // Its tags, with the ones this account's notes already carry on offer - private ones included,
        // since the store opens them here. Null tags are "not known", and stay unsaid until touched.
        await Tags.ShowAsync(
            note.Tags,
            (await _notes.GetAllAsync(cancellationToken)).SelectMany(stored => stored.AllTags),
            cancellationToken);

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
        _applying++;
        try
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
        }
        finally
        {
            _applying--;
        }

        RememberWhatIsWrittenDown();
        StartTheHistoryOver();
    }

    /// <summary>
    /// The history begins with the note as it was read in - unless this is the same note read back after
    /// a save, saying what the history already says. Changing how much a note matters or sealing it
    /// writes the note and reads it back, and that should not throw away the reader's undo.
    /// </summary>
    private void StartTheHistoryOver()
    {
        var shown = Surface(new SurfacePoint(0, 0));
        if (_historyOf == _localId && shown.Lines.SequenceEqual(_history.Current.Lines))
        {
            return;
        }

        _history.Reset(shown);
        _historyOf = _localId;
        SayWhatCanBeUndone();
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
    private void Watch(NoteLineRow row)
    {
        // A line that arrives while boxes are being chosen can be chosen too, once it has a box.
        row.OffersPicking = IsPickingLines;
        row.PropertyChanged += WhenALineChanges;
    }

    private void WhenALineChanges(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(NoteLineRow.IsPicked) or nameof(NoteLineRow.IsChecklistItem))
        {
            SayWhatIsPicked();
        }

        if (_applying > 0
            || args.PropertyName != nameof(NoteLineRow.Text)
            || sender is not NoteLineRow row
            || Lines.IndexOf(row) is var index && index < 0)
        {
            return;
        }

        // The surface's line: the note's name is line 0, so the first line of writing is line 1.
        var line = index + 1;
        var change = NoteTextChange.Between(row.TextBefore, row.Text);

        if (IsSeveralLines(change.Inserted))
        {
            Paste(line, change);
            return;
        }

        if (ReadPastedMarker(row, line, change))
        {
            return;
        }

        RecordTyping(line, change);
        ReadTypedMarker(row, line, change);
    }

    /// <summary>
    /// A line that now starts with "[]" (or "[ ]") after its indentation becomes a tick box, and the mark
    /// is taken back out. A step of its own in the history, as in the browser, so the first undo after it
    /// gives back the brackets rather than the whole word. The caret stays where it was in the words,
    /// which is further left now the mark has gone - and is put there, because the field's text is
    /// rewritten under it.
    /// </summary>
    private void ReadTypedMarker(NoteLineRow row, int line, NoteTextChange change)
    {
        var indentation = IndentationOf(row.Text);
        var rest = row.Text[indentation.Length..];

        // The browser's rule, from Orbit.Core: "[]" or "[ ]" - a phone keyboard puts a space inside the
        // brackets as readily as not - and one space after either, since "[] " and "[]" mean the same
        // thing and anything the reader typed beyond that is theirs.
        var eaten = NoteSurfaceEdits.TypedMarkerLength(rest);
        if (eaten == 0)
        {
            return;
        }
        var typedTo = change.Start + change.Inserted.Length;
        var caret = new SurfacePoint(line, Math.Max(indentation.Length, typedTo - eaten));

        Edit(SurfaceEditKind.Reshaping, new SurfacePoint(line, typedTo), () =>
        {
            row.IsChecklistItem = true;
            row.Text = indentation + rest[eaten..];
            return caret;
        });
        IsWritingAChecklist = true;
        PlaceCaret(caret);
    }

    /// <summary>
    /// Whether text that came into a field at once has a line break in it - a paste, since a one-line
    /// field's own Enter never puts one there (it raises Completed instead - see NoteDetailPage).
    /// </summary>
    private static bool IsSeveralLines(string inserted) => inserted.AsSpan().IndexOfAny('\n', '\r') >= 0;

    private static bool IsBlank(string text) => text.All(character => character is ' ' or '\t');

    /// <summary>
    /// A paste of several lines into one field: it becomes that many lines, at the caret, read the way
    /// the browser reads a paste (NoteSurfaceEdits.Replace with readsMarkers) - a line starting "[]",
    /// "[ ]" or "- " comes in as a box and "[x]" as a ticked one, the caret goes to the end of what was
    /// pasted, and all of it is one step of the history. A one-line field keeps a paste's line breaks in
    /// its text rather than acting on them, so this is where they are found and taken out.
    ///
    /// Two differences from the browser, both about what the phone has and the browser has not. A paste
    /// into the blank start of an indented line - where Enter leaves the caret on a line of a list -
    /// counts as starting that line, and the line keeps its indentation. And the note's name never
    /// becomes a box: it is the surface's first line, and there is no box beside it to draw.
    /// </summary>
    private void Paste(int line, NoteTextChange change)
    {
        var current = _history.Current;
        if (line >= current.Lines.Count)
        {
            RecordTyping(line, change);
            return;
        }

        var at = new SurfacePoint(line, change.Start + change.Removed);
        var before = current with { Anchor = at, Focus = at };

        var old = current.Lines[line];
        var lead = old.Text[..change.Start];
        var indented = line > 0 && lead.Length > 0 && IsBlank(lead);
        var lines = current.Lines.ToList();
        if (indented)
        {
            lines[line] = old with { Text = old.Text[change.Start..] };
        }

        var replacedFrom = new SurfacePoint(line, indented ? 0 : change.Start);
        var replacedTo = replacedFrom with { Offset = replacedFrom.Offset + change.Removed };
        var pasted = NoteSurfaceEdits.Replace(new SurfaceState(lines, replacedFrom, replacedTo), change.Inserted, readsMarkers: true);

        var result = pasted.Lines.ToList();
        if (indented)
        {
            result[line] = result[line] with { Text = lead + result[line].Text };
        }

        if (result[0].IsChecklistItem)
        {
            result[0] = NoteContentLine.PlainText(result[0].Text);
        }

        var after = pasted with { Lines = result };
        Show(after);
        Record(before, after.Caret, SurfaceEditKind.Pasting);
        PlaceCaret(after.Caret);
    }

    /// <summary>
    /// One line pasted at the head of a line that starts the way a box is written - "[]", "[ ]", "- ",
    /// or "[x]" for a ticked one - becomes that box, as a pasted line does in the browser, in one step of
    /// the history.
    ///
    /// More than one character arriving at once is what tells a paste from typing: a keyboard types one
    /// at a time. A word taken from the keyboard's suggestions arrives whole too, but no word starts with
    /// a mark. So typing "- " a key at a time stays words, as it does in the browser - a dash starts a
    /// line of prose as often as a list - and only the typed "[]" makes a box as it is typed (see
    /// <see cref="ReadTypedMarker"/>). Pasted after other words, or onto a line that is already a box, a
    /// mark is words.
    /// </summary>
    private bool ReadPastedMarker(NoteLineRow row, int line, NoteTextChange change)
    {
        if (change.Inserted.Length < 2 || row.IsChecklistItem || !IsBlank(row.Text[..change.Start]))
        {
            return false;
        }

        var indentation = IndentationOf(row.Text);
        var rest = row.Text[indentation.Length..];
        if (NoteSurfaceEdits.ReadPastedLine(rest) is not { IsChecklistItem: true } box)
        {
            return false;
        }

        var at = new SurfacePoint(line, change.Start + change.Removed);
        var before = _history.Current with { Anchor = at, Focus = at };
        var eaten = rest.Length - box.Text.Length;
        var caret = new SurfacePoint(line, Math.Max(indentation.Length, change.Start + change.Inserted.Length - eaten));

        _applying++;
        try
        {
            row.IsChecklistItem = true;
            row.IsChecked = box.IsChecked;
            row.Text = indentation + box.Text;
        }
        finally
        {
            _applying--;
        }

        Record(before, caret, SurfaceEditKind.Pasting);
        IsWritingAChecklist = true;
        PlaceCaret(caret);
        return true;
    }

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

    /// <summary>Whether the undo button has anything to do - never on a note that cannot be changed.</summary>
    public bool CanUndo => CanEdit && _history.CanUndo;

    /// <inheritdoc cref="CanUndo"/>
    public bool CanRedo => CanEdit && _history.CanRedo;

    /// <summary>
    /// Puts the note back as it was before the last step - see <see cref="_history"/> for what a step is.
    /// Nothing is written down: undo is a change to the screen like typing is, and Save still decides.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => PutBack(_history.Undo());

    /// <summary>Puts back the step last undone.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => PutBack(_history.Redo());

    private void PutBack(SurfaceState? state)
    {
        if (state is null)
        {
            return;
        }

        var wordsChanged = !Surface(state.Caret).Lines.Select(line => line.Text)
            .SequenceEqual(state.Lines.Select(line => line.Text));

        Show(state);
        SayWhatCanBeUndone();

        if (wordsChanged)
        {
            PlaceCaret(state.Caret);
        }
    }

    /// <summary>
    /// The note as the surface Orbit.Core decides edits on: its name first, then its lines - the shape
    /// Orbit.Web's editor has, where the name is the first line of the writing.
    /// </summary>
    private SurfaceState Surface(SurfacePoint caret)
        => SurfaceState.CaretAt([NoteContentLine.PlainText(Title), .. Lines.Select(line => line.ToLine())], caret).Normalized();

    /// <summary>
    /// Makes the screen say what <paramref name="state"/> says. Lines that already say it are left alone,
    /// and lines in the middle that changed are changed in place, so every field that can stay does -
    /// with its caret and its keyboard - and only lines that came or went are built or taken away.
    /// </summary>
    private void Show(SurfaceState state)
    {
        IReadOnlyList<NoteContentLine> wanted = state.Lines.Count > 1 ? [.. state.Lines.Skip(1)] : [SurfaceState.EmptyLine];

        _applying++;
        try
        {
            Title = state.Lines[0].Text;

            var same = 0;
            while (same < Lines.Count && same < wanted.Count && Lines[same].ToLine() == wanted[same])
            {
                same++;
            }

            var sameAtTheEnd = 0;
            while (sameAtTheEnd < Lines.Count - same
                && sameAtTheEnd < wanted.Count - same
                && Lines[Lines.Count - 1 - sameAtTheEnd].ToLine() == wanted[wanted.Count - 1 - sameAtTheEnd])
            {
                sameAtTheEnd++;
            }

            var shownBetween = Lines.Count - same - sameAtTheEnd;
            var wantedBetween = wanted.Count - same - sameAtTheEnd;
            var changedInPlace = Math.Min(shownBetween, wantedBetween);

            for (var offset = 0; offset < changedInPlace; offset++)
            {
                Lines[same + offset].Take(wanted[same + offset]);
            }

            for (var gone = changedInPlace; gone < shownBetween; gone++)
            {
                Lines[same + changedInPlace].PropertyChanged -= WhenALineChanges;
                Lines.RemoveAt(same + changedInPlace);
            }

            for (var added = changedInPlace; added < wantedBetween; added++)
            {
                var row = NoteLineRow.From(wanted[same + added]);
                Lines.Insert(same + added, row);
                Watch(row);
            }
        }
        finally
        {
            _applying--;
        }
    }

    /// <summary>Asks the page to put the caret at a point of the surface - see <see cref="CaretPlaced"/>.</summary>
    private void PlaceCaret(SurfacePoint point)
    {
        if (point.Line > Lines.Count)
        {
            return;
        }

        CaretPlaced?.Invoke(this, new NoteCaret(point.Line == 0 ? null : Lines[point.Line - 1], point.Offset));
    }

    /// <summary>
    /// Makes an edit to the lines and records it as one step of <paramref name="kind"/>. The caret points
    /// are where undoing it and redoing it put the caret back. Inside another edit, or while a note is
    /// being read in, it only makes the change - the outer one is the step.
    /// </summary>
    private void Edit(SurfaceEditKind kind, SurfacePoint caretBefore, Func<SurfacePoint> change)
    {
        if (_applying > 0)
        {
            change();
            return;
        }

        var before = Surface(caretBefore);
        SurfacePoint caretAfter;
        _applying++;
        try
        {
            caretAfter = change();
        }
        finally
        {
            _applying--;
        }

        Record(before, caretAfter, kind);
    }

    private void Record(SurfaceState before, SurfacePoint caretAfter, SurfaceEditKind kind, string? typed = null)
    {
        _history.Record(before, Surface(caretAfter), kind, Now(), typed);
        SayWhatCanBeUndone();
    }

    /// <summary>
    /// Characters typed into a line, or taken out of it, which the field has already done. Recorded on
    /// what the history last knew - the line as it was - with the caret where the change began, so that
    /// characters typed one after another join one step the way NoteSurfaceHistory joins them.
    /// </summary>
    private void RecordTyping(int line, NoteTextChange change)
    {
        var at = new SurfacePoint(line, change.Start + change.Removed);
        var before = _history.Current with { Anchor = at, Focus = at };
        var kind = change.Inserted.Length == 0 ? SurfaceEditKind.Erasing : SurfaceEditKind.Typing;
        Record(before, new SurfacePoint(line, change.Start + change.Inserted.Length), kind, change.Inserted);
    }

    /// <summary>
    /// The note's name is the first line of the writing, so typing in it is typing like any other - and
    /// several lines pasted into it leave the first as the name and the rest as the note's first lines.
    /// </summary>
    partial void OnTitleChanged(string? oldValue, string newValue)
    {
        if (_applying > 0)
        {
            return;
        }

        var change = NoteTextChange.Between(oldValue ?? string.Empty, newValue);
        if (IsSeveralLines(change.Inserted))
        {
            Paste(0, change);
            return;
        }

        RecordTyping(0, change);
    }

    /// <summary>A clock that only goes forward, in milliseconds - which is all the history asks of one.</summary>
    private double Now() => _timeProvider.GetTimestamp() * 1000d / _timeProvider.TimestampFrequency;

    private void SayWhatCanBeUndone()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Whether boxes are being chosen to change together with one press. The browser chooses them with
    /// Shift+click, which a phone has no way to do, so here "Select boxes" in the note's menu turns this
    /// on: a mark stands beside every box (NoteLineRow.ShowsPickMark), and a press on one of two or more
    /// chosen boxes gives every chosen box that box's next answer - see ToggleChecked. The chosen boxes
    /// stay chosen after a press, as the browser's selection does, so a second press carries on with them.
    /// </summary>
    [ObservableProperty]
    private bool _isPickingLines;

    /// <summary>Whether there is anything to choose: two boxes, on a note that can be changed.</summary>
    public bool CanPickLines => CanEdit && Lines.Count(line => line.IsChecklistItem) >= 2;

    /// <summary>
    /// The line over the note while boxes are being chosen: how many are, and what a press on one of them
    /// does - the phone's counterpart of the bubble over the browser's tools.
    /// </summary>
    public string PickingHint
        => PickedLines() is { Count: >= 2 } picked
            ? _translations.Format("{0} selected - pressing one of their boxes sets them all.", picked.Count)
            : _translations["Select the boxes to change together, then press one of them."];

    [RelayCommand(CanExecute = nameof(CanPickLines))]
    private void StartPickingLines() => IsPickingLines = true;

    /// <summary>Stops choosing and lets every chosen box go.</summary>
    [RelayCommand]
    private void StopPickingLines() => IsPickingLines = false;

    /// <summary>The chosen boxes, as their places among the lines.</summary>
    private IReadOnlyList<int> PickedLines()
        => [.. Lines.Select((line, index) => (line, index))
            .Where(candidate => candidate.line is { IsPicked: true, IsChecklistItem: true })
            .Select(candidate => candidate.index)];

    partial void OnIsPickingLinesChanged(bool value)
    {
        foreach (var line in Lines)
        {
            line.OffersPicking = value;
            if (!value)
            {
                line.IsPicked = false;
            }
        }

        SayWhatIsPicked();
    }

    private void SayWhatIsPicked()
    {
        OnPropertyChanged(nameof(PickingHint));
        OnPropertyChanged(nameof(CanPickLines));
        StartPickingLinesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        // The tags box answers to the same rule as every other field here.
        Tags.IsReadOnly = value;
        SayWhatCanBeUndone();

        // Nothing to change together on a note that cannot be changed.
        if (value)
        {
            IsPickingLines = false;
        }

        SayWhatIsPicked();
    }

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
