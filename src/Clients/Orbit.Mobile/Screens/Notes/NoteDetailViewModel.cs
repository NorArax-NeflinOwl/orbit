using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notes;
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
    /// Only its owner may ever read this note, and the server never can. Orbit.Web's editor has had the
    /// checkbox all along; the phone honoured the flag - hiding such a note behind the device lock - but
    /// could not set one, so a note made here could never be private.
    /// </summary>
    [ObservableProperty]
    private bool _isPrivate;

    public NoteDetailViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, NotesClient notesClient, EditLock editLock,
        Translations translations, PrivateContentSealer privateContent, SharePanel share, IScreenNavigator navigator)
    {
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

    [ObservableProperty]
    private Tasks.PriorityChoice _chosenPriority;

    /// <inheritdoc cref="Tasks.TaskListDetailViewModel"/>
    private string _priority = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    public ObservableCollection<NoteLineRow> Lines { get; } = [];

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    /// <summary>
    /// Whether there is anything to offer anybody. A private note is offered to nobody - the server
    /// holds no readable copy to hand over, which is what makes it private - and a note the server has
    /// never seen cannot be named in a share, because a share names it by its server id.
    /// </summary>
    public bool CanBeShared => _isOnTheServer && !IsPrivate;

    private bool _isOnTheServer;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredNoteAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private Task AddLineAsync(CancellationToken cancellationToken)
    {
        var text = NewLine.Trim();
        NewLine = string.Empty;

        return SaveAsync([.. Lines.Select(line => line.ToDto()), new NoteContentLineDto(text, false, false)], cancellationToken);
    }

    private bool CanAddLine => NewLine.Trim().Length > 0 && CanEdit;

    /// <summary>
    /// A new line that starts out tickable, which is what Orbit.Web's "Checklist item" toolbar button
    /// does. Without it the only way to get one here was to add prose and then convert it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private Task AddChecklistItemAsync(CancellationToken cancellationToken)
    {
        var text = NewLine.Trim();
        NewLine = string.Empty;

        return SaveAsync([.. Lines.Select(line => line.ToDto()), new NoteContentLineDto(text, true, false)], cancellationToken);
    }

    /// <summary>
    /// Writes down what the lines say now. Every other action here saves the whole note anyway, so this
    /// is for the one case that would otherwise be lost: a line edited and then left alone.
    /// </summary>
    [RelayCommand]
    private Task SaveLinesAsync(CancellationToken cancellationToken)
        => CanEdit ? SaveAsync([.. Lines.Select(line => line.ToDto())], cancellationToken) : Task.CompletedTask;

    /// <summary>Turns a line into a checklist item, or back into an ordinary one.</summary>
    [RelayCommand]
    private Task ToggleChecklistAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync(
                [.. Lines.Select(line => ReferenceEquals(line, row)
                    ? line.ToDto() with { IsChecklistItem = !line.IsChecklistItem, IsChecked = false }
                    : line.ToDto())],
                cancellationToken);

    [RelayCommand]
    private Task ToggleCheckedAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is not { IsChecklistItem: true }
            ? Task.CompletedTask
            : SaveAsync(
                [.. Lines.Select(line => ReferenceEquals(line, row)
                    ? line.ToDto() with { IsChecked = !line.IsChecked }
                    : line.ToDto())],
                cancellationToken);

    [RelayCommand]
    private Task RemoveLineAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync([.. Lines.Where(line => !ReferenceEquals(line, row)).Select(line => line.ToDto())], cancellationToken);

    /// <summary>Renaming saves the whole note, because the API's update takes the whole note.</summary>
    [RelayCommand]
    private Task RenameAsync(CancellationToken cancellationToken)
        => SaveAsync([.. Lines.Select(line => line.ToDto())], cancellationToken);

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (await _notes.DeleteAsync(_localId, cancellationToken) is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowNotes();
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    private async Task SaveAsync(IReadOnlyList<NoteContentLineDto> lines, CancellationToken cancellationToken)
    {
        try
        {
            if (await _notes.UpdateAsync(
                    _localId, new NoteContent(Title.Trim(), lines, _priority, IsPrivate), cancellationToken)
                is LocalWriteOutcome.RefusedWhileOffline)
            {
                Status = _translations[RefusalMessage];
                return;
            }
        }
        catch (EncryptionKeyLockedException)
        {
            // Sealing needs the account's own key, and this device has not got it. The key gate is where
            // that is fixed, and it is where chat sends people for the same reason.
            _navigator.ShowChatKeyGate();
            return;
        }

        await ShowStoredNoteAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task ShowStoredNoteAsync(CancellationToken cancellationToken)
    {
        if (await _notes.FindAsync(_localId, cancellationToken) is not { } note)
        {
            _navigator.ShowNotes();
            return;
        }

        Title = note.Title;
        _isShowingWhatIsStored = true;
        ChosenPriority = Tasks.PriorityChoice.For(note.Priority, _translations);
        IsPrivate = note.IsPrivate;
        _isShowingWhatIsStored = false;

        // Only a note the server knows about can be offered: a share names it by its server id, and one
        // still waiting in the outbox has none. A private note is offered to nobody - the server holds
        // no readable copy to hand over, which is what makes it private.
        _isOnTheServer = note.ServerId is not null;
        OnPropertyChanged(nameof(CanBeShared));
        if (note is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(SharedItemKind.Note, serverId, note.Title, OwnerToAsk(note));
        }

        await ShowWhetherItCanBeChangedAsync(note, cancellationToken);

        Lines.Clear();
        foreach (var line in note.Content)
        {
            Lines.Add(NoteLineRow.From(line));
        }
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
        ReadOnlyReason = IsReadOnly ? _translations[RefusalMessage] : string.Empty;

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
    /// Lets the note go when the screen does, rather than leaving it claimed for a minute - and writes
    /// down anything typed into a line and not otherwise saved before letting go of it.
    /// </summary>
    public async Task CloseAsync()
    {
        await SaveLinesAsync(CancellationToken.None);
        await _editLock.ReleaseAsync();
    }

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
            SaveLinesCommand.Execute(null);
        }
    }

    /// <inheritdoc cref="OnChosenPriorityChanged"/>
    partial void OnIsPrivateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanBeShared));
        if (!_isShowingWhatIsStored && CanEdit)
        {
            SaveLinesCommand.Execute(null);
        }
    }

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        AddLineCommand.NotifyCanExecuteChanged();
        AddChecklistItemCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewLineChanged(string value)
    {
        AddLineCommand.NotifyCanExecuteChanged();
        AddChecklistItemCommand.NotifyCanExecuteChanged();
    }
}
