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
}

/// <summary>
/// Where a copy kept by a review goes to be found again - the "Historia" window from the offline work
/// order. It answers one question no list can: this and that are two versions of the same thing, and
/// here is which came from which.
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

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var kept = new List<CopyUnderReview>();
        foreach (var store in _stores)
        {
            kept.AddRange(await store.GetKeptCopiesAsync(cancellationToken));
        }

        Rows.Clear();
        foreach (var copy in kept.OrderByDescending(candidate => candidate.CopiedAtUtc))
        {
            Rows.Add(Describe(copy));
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

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

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
            OriginalLocalId = copy.OriginalLines is null ? null : copy.OriginalLocalId
        };
    }
}
