using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One note and what it says. The counterpart of <see cref="Tasks.TaskListDetailViewModel"/> and shaped
/// the same way: every change is written to the local database first and queued from there, so writing
/// works with no connection and the screen never waits on a request.
///
/// <b>A private note opens read-only</b>, and not as a limitation of this screen. Its words live inside
/// an encrypted payload the phone has no key for - the server sends an empty title and no lines at all
/// (see Orbit.Core.Notes.Note.ReadableOrSealed) - so there is nothing here to show and nothing that
/// could be sent back. Saying so is the only honest thing to do until the phone can hold that key.
/// </summary>
public sealed partial class NoteDetailViewModel : ObservableObject
{
    private readonly LocalNoteRepository _notes;
    private readonly NoteSynchronizer _synchronizer;
    private readonly Translations _translations;
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

    /// <summary>Why it cannot be changed, when it cannot - a private note, or somebody else's while offline.</summary>
    [ObservableProperty]
    private string _readOnlyReason = string.Empty;

    public NoteDetailViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator)
    {
        _notes = notes;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
    }

    public ObservableCollection<NoteLineRow> Lines { get; } = [];

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

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

    /// <summary>Turns a line into a checklist item, or back into an ordinary one.</summary>
    [RelayCommand]
    private Task ToggleChecklistAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync(
                [.. Lines.Select(line => line == row
                    ? line.ToDto() with { IsChecklistItem = !line.IsChecklistItem, IsChecked = false }
                    : line.ToDto())],
                cancellationToken);

    [RelayCommand]
    private Task ToggleCheckedAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is not { IsChecklistItem: true }
            ? Task.CompletedTask
            : SaveAsync(
                [.. Lines.Select(line => line == row ? line.ToDto() with { IsChecked = !line.IsChecked } : line.ToDto())],
                cancellationToken);

    [RelayCommand]
    private Task RemoveLineAsync(NoteLineRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync([.. Lines.Where(line => line != row).Select(line => line.ToDto())], cancellationToken);

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
        if (await _notes.UpdateAsync(_localId, Title.Trim(), lines, cancellationToken)
            is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
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

        // Only a note the server knows about can be offered: a share names it by its server id, and one
        // still waiting in the outbox has none.
        if (note.ServerId is { } serverId)
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
        if (note.IsPrivate)
        {
            IsReadOnly = true;
            ReadOnlyReason = _translations["This note is private, and its words are sealed with a key this phone doesn't have."];
            return;
        }

        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        IsReadOnly = !await _notes.CanEditAsync(_localId, cancellationToken);
        ReadOnlyReason = IsReadOnly ? _translations[RefusalMessage] : string.Empty;
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

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        AddLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewLineChanged(string value) => AddLineCommand.NotifyCanExecuteChanged();
}
