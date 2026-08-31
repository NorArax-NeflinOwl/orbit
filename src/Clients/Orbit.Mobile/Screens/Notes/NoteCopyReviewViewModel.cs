using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One copy taken offline, beside the note it came from as that note stands now. Everything the reader
/// needs to answer the only question there is: which of these two is the note from here on.
/// </summary>
public sealed class CopyReviewRow
{
    public required Guid LocalId { get; init; }

    public required string Title { get; init; }

    /// <summary>What was written into the copy - the diff from the note as it was when it was taken.</summary>
    public required IReadOnlyList<DiffLine> MyChanges { get; init; }

    /// <summary>What happened to the original in the meantime, over the same starting point.</summary>
    public required IReadOnlyList<DiffLine> TheirChanges { get; init; }

    /// <summary>
    /// Both sides moved. Not an error and not a failure - just the case where keeping one version
    /// throws the other away, which is worth saying out loud before somebody taps.
    /// </summary>
    public required bool HasConflict { get; init; }

    /// <summary>
    /// The note this was copied from is no longer here - deleted while the phone was away. There is
    /// nothing to apply the copy over, so the copy is all that is left of it and keeping it is the only
    /// answer that does not lose the words.
    /// </summary>
    public required bool IsOriginalGone { get; init; }

    public bool HasTheirChanges => TheirChanges.Any(line => line.Change is not LineChange.Unchanged);
}

/// <summary>
/// The window a reader gets when they come back online holding copies they made while away, from
/// info/orbit-maui-plan.md §5.4. Three answers, no more: keep what I wrote, keep what is there, or keep
/// both - and "both" is what the History screen then lists.
///
/// It reads and writes through <see cref="LocalNoteRepository"/> like every other screen, so applying a
/// copy is queued and pushed by the ordinary outbox. A review is an edit made late, not a special path
/// into the server.
/// </summary>
public sealed partial class NoteCopyReviewViewModel : ObservableObject
{
    private readonly LocalNoteRepository _notes;
    private readonly NoteSynchronizer _synchronizer;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _message = string.Empty;

    public NoteCopyReviewViewModel(
        LocalNoteRepository notes, NoteSynchronizer synchronizer, Translations translations,
        IScreenNavigator navigator)
    {
        _notes = notes;
        _synchronizer = synchronizer;
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<CopyReviewRow> Reviews { get; } = [];

    public bool HasMessage => Message.Length > 0;

    public bool HasNothingToReview => Reviews.Count == 0;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Reviews.Clear();
        foreach (var copy in await _notes.GetCopiesAwaitingReviewAsync(cancellationToken))
        {
            Reviews.Add(await DescribeAsync(copy, cancellationToken));
        }

        OnPropertyChanged(nameof(HasNothingToReview));
    }

    /// <summary>Keeps what was written offline, over the note it was copied from.</summary>
    [RelayCommand]
    private Task KeepMineAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => row is null ? Task.CompletedTask : ResolveAsync(_notes.ApplyCopyAsync(row.LocalId, cancellationToken), cancellationToken);

    /// <summary>Keeps the note as it stands and drops the copy - the answer for work already done twice.</summary>
    [RelayCommand]
    private Task KeepTheirsAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => row is null ? Task.CompletedTask : ResolveAsync(_notes.DiscardCopyAsync(row.LocalId, cancellationToken), cancellationToken);

    /// <summary>
    /// Keeps both. The copy becomes a note of its own, tagged as a copy and still pointing at what it
    /// came from, which is what the History screen reads.
    /// </summary>
    [RelayCommand]
    private Task KeepBothAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => row is null ? Task.CompletedTask : ResolveAsync(_notes.KeepCopyAsync(row.LocalId, cancellationToken), cancellationToken);

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    private async Task ResolveAsync(Task<LocalWriteOutcome> write, CancellationToken cancellationToken)
    {
        Message = await write switch
        {
            LocalWriteOutcome.RefusedWhileOffline => _translations[
                "Orbit can't be reached to check who else is editing. Try this again once you're back online."],
            LocalWriteOutcome.NotFound => _translations["That note is no longer here."],
            _ => string.Empty
        };

        await LoadAsync(cancellationToken);
        await PushAsync(cancellationToken);
    }

    /// <summary>
    /// Sends what the review just queued. Nothing is lost when it cannot go - it is in the outbox - so
    /// this says nothing on failure: the reader has just answered a question about being offline.
    /// </summary>
    private async Task PushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _synchronizer.SynchroniseAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
        }
    }

    private async Task<CopyReviewRow> DescribeAsync(LocalNote copy, CancellationToken cancellationToken)
    {
        var original = copy.CopyOfLocalId is { } originalLocalId
            ? await _notes.FindAsync(originalLocalId, cancellationToken)
            : null;

        var theirChanges = original is null
            ? []
            : NoteVersionDiff.Between(copy.CopyBaseContent, original.Content);

        return new CopyReviewRow
        {
            LocalId = copy.LocalId,
            Title = copy.Title,
            MyChanges = NoteVersionDiff.Between(copy.CopyBaseContent, copy.Content),
            TheirChanges = theirChanges,
            HasConflict = original is not null && NoteVersionDiff.Differ(copy.CopyBaseContent, original.Content),
            IsOriginalGone = original is null
        };
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
