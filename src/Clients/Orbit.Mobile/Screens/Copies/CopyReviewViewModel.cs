using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Copies;

/// <summary>
/// One copy taken offline, beside the thing it came from as that thing stands now. Everything the
/// reader needs to answer the only question there is: which of these two is it from here on.
/// </summary>
public sealed class CopyReviewRow
{
    public required CopyKind Kind { get; init; }

    public required Guid LocalId { get; init; }

    public required string Title { get; init; }

    /// <summary>Which kind this is, in words - one screen holds all four, so each row says which it is.</summary>
    public required string KindDescription { get; init; }

    /// <summary>What was written into the copy - the diff from what it said when it was taken.</summary>
    public required IReadOnlyList<DiffLine> MyChanges { get; init; }

    /// <summary>What happened to the original in the meantime, over the same starting point.</summary>
    public required IReadOnlyList<DiffLine> TheirChanges { get; init; }

    /// <summary>
    /// Both sides moved. Not an error and not a failure - just the case where keeping one version
    /// throws the other away, which is worth saying out loud before somebody taps.
    /// </summary>
    public required bool HasConflict { get; init; }

    /// <summary>
    /// What this was copied from is no longer here - its owner deleted it while the phone was away.
    /// There is nothing left to choose between, so the card asks the one question that remains: keep
    /// this copy, or not. Keeping it makes it the thing itself, and it keeps its copy tag and its place
    /// in that thing's history so the reader can still see where it came from.
    /// </summary>
    public required bool IsOriginalGone { get; init; }

    /// <summary>Whether there are two versions to choose between, which is what the three answers are for.</summary>
    public bool IsDecidable => !IsOriginalGone;

    public bool HasTheirChanges => TheirChanges.Any(line => line.Change is not LineChange.Unchanged);
}

/// <summary>
/// The window a reader gets when they come back online holding copies they made while away, from
/// info/orbit-maui-plan.md §5.4. Three answers, no more: keep what I wrote, keep what is there, or keep
/// both - and "both" is what the History screen then lists.
///
/// One screen for all four kinds rather than four screens: it asks the same question about a note, a
/// task list, an appointment and an inventory, and somebody coming back from a week away wants one place
/// that says what is outstanding, not four to remember to visit.
///
/// It reads and writes through the repositories like every other screen, so applying a copy is queued
/// and pushed by the ordinary outbox. A review is an edit made late, not a special path to the server.
/// </summary>
public sealed partial class CopyReviewViewModel : ObservableObject
{
    private readonly IReadOnlyList<ICopyReviewStore> _stores;
    private readonly EverythingSynchronizer _synchronizer;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _message = string.Empty;

    public CopyReviewViewModel(
        IEnumerable<ICopyReviewStore> stores, EverythingSynchronizer synchronizer, Translations translations,
        IScreenNavigator navigator)
    {
        _stores = [.. stores];
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
        var waiting = new List<(ICopyReviewStore Store, CopyUnderReview Copy)>();
        foreach (var store in _stores)
        {
            waiting.AddRange(
                (await store.GetCopiesAwaitingReviewAsync(cancellationToken)).Select(copy => (store, copy)));
        }

        Reviews.Clear();
        // Newest first across all four, so the screen reads as one list rather than four appended.
        foreach (var (_, copy) in waiting.OrderByDescending(pair => pair.Copy.CopiedAtUtc))
        {
            Reviews.Add(Describe(copy));
        }

        OnPropertyChanged(nameof(HasNothingToReview));
    }

    /// <summary>Keeps what was written offline, over the thing it was copied from.</summary>
    [RelayCommand]
    private Task KeepMineAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => ResolveAsync(row, (store, copy) => store.ApplyCopyAsync(copy, cancellationToken), cancellationToken);

    /// <summary>Keeps it as it stands and drops the copy - the answer for work already done twice.</summary>
    [RelayCommand]
    private Task KeepTheirsAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => ResolveAsync(row, (store, copy) => store.DiscardCopyAsync(copy, cancellationToken), cancellationToken);

    /// <summary>
    /// Keeps both. The copy becomes a thing of its own, tagged as a copy and still pointing at what it
    /// came from, which is what the History screen reads.
    /// </summary>
    [RelayCommand]
    private Task KeepBothAsync(CopyReviewRow? row, CancellationToken cancellationToken)
        => ResolveAsync(row, (store, copy) => store.KeepCopyAsync(copy, cancellationToken), cancellationToken);

    /// <summary>Opens the copy itself, for a reader who wants to read it before deciding.</summary>
    [RelayCommand]
    private void Open(CopyReviewRow? row)
    {
        if (row is not null)
        {
            CopyDestination.Show(_navigator, row.Kind, row.LocalId);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    private async Task ResolveAsync(
        CopyReviewRow? row, Func<ICopyReviewStore, Guid, Task<LocalWriteOutcome>> answer,
        CancellationToken cancellationToken)
    {
        if (row is null || _stores.FirstOrDefault(candidate => candidate.Kind == row.Kind) is not { } store)
        {
            return;
        }

        Message = await answer(store, row.LocalId) switch
        {
            LocalWriteOutcome.RefusedWhileOffline => _translations[
                "Orbit can't be reached to check who else is editing. Try this again once you're back online."],
            // A copy of something shared read-only: keeping it cannot replace the original, whatever the
            // connection is like - see SharedItemAccess.
            LocalWriteOutcome.RefusedAsReadOnly => _translations[
                "Shared with you to read. Ask whoever shared it if you need to change it."],
            LocalWriteOutcome.NotFound => _translations["That is no longer here."],
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

    private CopyReviewRow Describe(CopyUnderReview copy)
        => new()
        {
            Kind = copy.Kind,
            LocalId = copy.LocalId,
            Title = copy.Title,
            KindDescription = _translations[CopyDestination.Describe(copy.Kind)],
            MyChanges = VersionDiff.Between(copy.BaseLines, copy.Lines),
            TheirChanges = copy.OriginalLines is null ? [] : VersionDiff.Between(copy.BaseLines, copy.OriginalLines),
            HasConflict = copy.OriginalLines is not null && VersionDiff.Differ(copy.BaseLines, copy.OriginalLines),
            IsOriginalGone = copy.OriginalLines is null
        };

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
