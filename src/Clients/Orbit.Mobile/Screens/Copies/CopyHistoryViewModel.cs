using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Copies;

/// <summary>One kept copy, and the thing it was taken from - the reference the History window is for.</summary>
public sealed class HistoryRow
{
    public required CopyKind Kind { get; init; }

    public required Guid LocalId { get; init; }

    public required string Title { get; init; }

    /// <summary>When it was taken and what from, in words - which is what makes it findable.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// What this came from, when it is still here. Null once it has been deleted: the copy outlives it,
    /// and offering a way to something that is gone would be a dead end rather than a reference.
    /// </summary>
    public Guid? OriginalLocalId { get; init; }

    public bool HasOriginal => OriginalLocalId is not null;

    /// <summary>
    /// Still a question rather than a decision - the history shows both, because a copy sitting
    /// unanswered is part of what happened to this thing just as much as one that was kept.
    /// </summary>
    public bool IsAwaitingReview { get; init; }
}

/// <summary>
/// One thing's history: the copies taken from it, and where each came from.
///
/// Per thing rather than one list of everything ever kept. History is a fact about a note or a list, not
/// about the account - somebody looking at two lists called "Zakupy" wants that pair's story, and a
/// global window would make them read past everything else to find it. Opened from the thing itself,
/// and it shows the same story whichever of the two versions was open.
/// </summary>
public sealed partial class CopyHistoryViewModel : ObservableObject
{
    private readonly IReadOnlyList<ICopyReviewStore> _stores;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    public CopyHistoryViewModel(
        IEnumerable<ICopyReviewStore> stores, Translations translations, IScreenNavigator navigator)
    {
        _stores = [.. stores];
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<HistoryRow> Rows { get; } = [];

    public bool HasNothing => Rows.Count == 0;

    /// <summary>What the thing is called, so the window says whose history this is.</summary>
    [ObservableProperty]
    private string _subjectTitle = string.Empty;

    private CopyKind _kind;

    private Guid _localId;

    /// <summary>Which thing's history to show - see the class summary for why it is one thing's.</summary>
    public void Open(CopyKind kind, Guid localId)
    {
        _kind = kind;
        _localId = localId;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Rows.Clear();
        if (_stores.FirstOrDefault(candidate => candidate.Kind == _kind) is { } store)
        {
            foreach (var copy in await store.GetHistoryOfAsync(_localId, cancellationToken))
            {
                SubjectTitle = copy.Title;
                Rows.Add(Describe(copy));
            }
        }

        OnPropertyChanged(nameof(HasNothing));
    }

    /// <summary>The row is the button, as everywhere else - tapping a copy opens it.</summary>
    [RelayCommand]
    private void Open(HistoryRow? row)
    {
        if (row is not null)
        {
            CopyDestination.Show(_navigator, row.Kind, row.LocalId);
        }
    }

    [RelayCommand]
    private void OpenOriginal(HistoryRow? row)
    {
        if (row is { OriginalLocalId: { } originalLocalId })
        {
            CopyDestination.Show(_navigator, row.Kind, originalLocalId);
        }
    }

    /// <summary>Back to the thing this is the history of, which is where it was opened from.</summary>
    [RelayCommand]
    private void GoBack() => CopyDestination.Show(_navigator, _kind, _localId);

    private HistoryRow Describe(CopyUnderReview copy)
    {
        var takenOn = copy.CopiedAtUtc.ToLocalTime().ToString("d MMM yyyy", _translations.DisplayCulture);
        var kind = _translations[CopyDestination.Describe(copy.Kind)];

        return new HistoryRow
        {
            Kind = copy.Kind,
            LocalId = copy.LocalId,
            Title = copy.Title,
            Description = copy.OriginalLines is null
                ? string.Format(_translations["{0} · copied on {1}. What it came from is gone."], kind, takenOn)
                : string.Format(_translations["{0} · copy of “{1}”, made on {2}."], kind, copy.Title, takenOn),
            IsAwaitingReview = !copy.IsKept,
            OriginalLocalId = copy.OriginalLines is null ? null : copy.OriginalLocalId
        };
    }
}
