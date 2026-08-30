using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Suggestions;

/// <summary>
/// The names already in this account, offered under the field being typed into - the phone's half of
/// Orbit.Web's NameSuggestions component, with the same two jobs told apart by how close the match is:
/// a completion to save typing, and a warning that what is being typed is a name the reader already
/// has, about to become the same thing twice.
///
/// Held by the editor that is open rather than by the screen, because an editor is the only thing that
/// knows which of the four fields is being typed into.
/// </summary>
public sealed partial class NameSuggestions : ObservableObject
{
    /// <summary>
    /// How long to wait after the last keystroke. Long enough that typing a word is one lookup rather
    /// than eight, short enough that the list is there by the time somebody stops to look at it. The
    /// number Orbit.Web waits, for the same reasons.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>Above this the two names are the same thing spelled differently - see the query handler.</summary>
    private const double DuplicateSimilarity = 0.6;

    /// <summary>Below this everything looks similar to everything - the rule the query handler applies too.</summary>
    private const int ShortestUsefulQuery = 2;

    private readonly SuggestionsClient _suggestions;
    private readonly Translations _translations;

    private NameSuggestionKind _kind;
    private string _lastLookedUp = string.Empty;
    private CancellationTokenSource? _pending;

    public NameSuggestions(SuggestionsClient suggestions, Translations translations)
    {
        _suggestions = suggestions;
        _translations = translations;
    }

    /// <summary>The names on offer, from the newest lookup only.</summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>
    /// Said out loud rather than left to be spotted: what is being typed is a name the reader already
    /// has. Empty when nothing on offer is that close.
    /// </summary>
    [ObservableProperty]
    private string _duplicateWarning = string.Empty;

    public bool HasAny => Names.Count > 0;

    public bool HasDuplicateWarning => DuplicateWarning.Length > 0;

    /// <summary>
    /// Where a chosen name goes. One target rather than an event, because one field is being typed into
    /// at a time - and an event left every editor that had ever been open still listening, so a name
    /// picked in the second one was also taken by the first, which was no longer on screen.
    /// </summary>
    public Action<string>? Takes { get; set; }

    /// <summary>Which field these are for, and so which names are offered - see NameSuggestionKind.</summary>
    public void Offers(NameSuggestionKind kind) => _kind = kind;

    /// <summary>
    /// Looks up what is being typed, once the typing has stopped. Started rather than awaited: this
    /// hangs off a property setter, and a field that waited for the network between keystrokes would be
    /// unusable.
    /// </summary>
    public void ShowFor(string typed)
    {
        var wanted = typed.Trim();
        if (wanted == _lastLookedUp)
        {
            return;
        }

        _lastLookedUp = wanted;
        Cancel();

        if (wanted.Length < ShortestUsefulQuery)
        {
            Show([]);
            return;
        }

        _pending = new CancellationTokenSource();
        _ = LookUpAsync(wanted, _pending.Token);
    }

    /// <summary>
    /// The value the field is opening on, taken as already looked up. Suggestions are about what
    /// somebody is typing, not about what is already saved - without this, opening an item to change
    /// its expiry date would offer completions of its own name and warn that it duplicates itself.
    /// </summary>
    public void StartsAt(string value)
    {
        Cancel();
        _lastLookedUp = value.Trim();
        Show([]);
    }

    /// <summary>Clears what is on offer - said when an editor closes, so the next one opens quiet.</summary>
    public void Forget()
    {
        Cancel();
        _lastLookedUp = string.Empty;
        Show([]);
    }

    [RelayCommand]
    private void Choose(string? name)
    {
        if (name is not { Length: > 0 })
        {
            return;
        }

        // Counted as already looked up, so the name just taken is not offered straight back as a
        // duplicate of itself the moment the field holds it.
        _lastLookedUp = name;
        Cancel();
        Show([]);
        Takes?.Invoke(name);
    }

    private async Task LookUpAsync(string wanted, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SettleDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var found = await _suggestions.FindAsync(_kind, wanted, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        Show([.. found.Select(suggestion => suggestion.Name)]);
        DuplicateWarning = found.FirstOrDefault(suggestion => suggestion.Similarity >= DuplicateSimilarity) is { } duplicate
            ? _translations.Format("You already have \"{0}\".", duplicate.Name)
            : string.Empty;
    }

    private void Cancel()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = null;
    }

    private void Show(IReadOnlyList<string> names)
    {
        Names.Clear();
        foreach (var name in names)
        {
            Names.Add(name);
        }

        if (names.Count == 0)
        {
            DuplicateWarning = string.Empty;
        }

        OnPropertyChanged(nameof(HasAny));
    }

    partial void OnDuplicateWarningChanged(string value) => OnPropertyChanged(nameof(HasDuplicateWarning));
}
