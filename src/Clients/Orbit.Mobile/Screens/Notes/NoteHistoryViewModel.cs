using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>One kept copy, and the note it was taken from - the reference the History window is for.</summary>
public sealed class HistoryRow
{
    public required Guid LocalId { get; init; }

    public required string Title { get; init; }

    /// <summary>When it was taken, in words - "kept from X on 3 Sep", which is what makes it findable.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// The note this came from, when it is still here. Null once it has been deleted: the copy outlives
    /// it, and offering a way to a note that is gone would be a dead end rather than a reference.
    /// </summary>
    public Guid? OriginalLocalId { get; init; }

    public bool HasOriginal => OriginalLocalId is not null;
}

/// <summary>
/// Where a copy kept by a review goes to be found again - the "Historia" window from the offline work
/// order. It answers one question the notes list cannot: this note and that note are two versions of
/// the same thing, and here is which came from which.
/// </summary>
public sealed partial class NoteHistoryViewModel : ObservableObject
{
    private readonly LocalNoteRepository _notes;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    public NoteHistoryViewModel(LocalNoteRepository notes, Translations translations, IScreenNavigator navigator)
    {
        _notes = notes;
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<HistoryRow> Rows { get; } = [];

    public bool HasNothing => Rows.Count == 0;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Rows.Clear();
        foreach (var copy in await _notes.GetKeptCopiesAsync(cancellationToken))
        {
            Rows.Add(await DescribeAsync(copy, cancellationToken));
        }

        OnPropertyChanged(nameof(HasNothing));
    }

    /// <summary>The row is the button, as everywhere else - tapping a copy opens it.</summary>
    [RelayCommand]
    private void Open(HistoryRow? row)
    {
        if (row is not null)
        {
            _navigator.ShowNote(row.LocalId);
        }
    }

    [RelayCommand]
    private void OpenOriginal(HistoryRow? row)
    {
        if (row is { OriginalLocalId: { } originalLocalId })
        {
            _navigator.ShowNote(originalLocalId);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    private async Task<HistoryRow> DescribeAsync(LocalNote copy, CancellationToken cancellationToken)
    {
        var original = copy.CopyOfLocalId is { } originalLocalId
            ? await _notes.FindAsync(originalLocalId, cancellationToken)
            : null;

        var takenOn = copy.CopiedAtUtc?.LocalDateTime.ToString("d MMM yyyy") ?? string.Empty;

        return new HistoryRow
        {
            LocalId = copy.LocalId,
            Title = copy.Title,
            Description = original is null
                ? string.Format(_translations["Copied on {0}. The note it came from is gone."], takenOn)
                : string.Format(_translations["Copy of “{0}”, made on {1}."], original.Title, takenOn),
            OriginalLocalId = original?.LocalId
        };
    }
}
