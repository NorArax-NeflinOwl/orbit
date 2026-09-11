using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Tags;
using Orbit.Core.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tags;

/// <summary>
/// The tags of a note or a task list while its screen is open - the phone's half of Orbit.Web's TagsField.
/// The same kind of field this phone gives an entry's categories: one line, words separated by commas (see
/// CategoryText), with the tags this account already uses offered as chips to tap. Under it, one row per tag
/// with a palette, because a colour belongs to the tag for the whole account (see LocalTagColourRepository):
/// it is set there and then, on this phone first and on the server at the next sync, rather than saved with
/// the item.
/// </summary>
public sealed partial class TagsForm : ObservableObject
{
    /// <summary>
    /// What a tag can be coloured - eight colours a thumb can tell apart, each light enough to wash behind a
    /// word and dark enough to edge one, in either theme. The web offers any colour at all; a phone offers
    /// these, and a colour chosen in a browser that is not among them is still drawn here as chosen.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
        ["#d9534f", "#e8903c", "#d4b020", "#4caf6a", "#2f9fb0", "#4a7fd6", "#8e6bd6", "#c4569a"];

    /// <summary>How many tags already in use are offered at once - the number the web's panel stops at.</summary>
    private const int MostOffered = 12;

    private readonly Translations _translations;
    private readonly LocalTagColourRepository? _colours;
    private readonly TagColourSynchronizer? _synchronizer;

    private IReadOnlyList<string> _known = [];
    private IReadOnlyDictionary<string, string> _colourMap = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _wasKnown;
    private bool _isTouched;
    private bool _isShowing;

    /// <param name="colours">Where the account's colours are kept, or null for a form that only takes words.</param>
    /// <param name="synchronizer">Sends a colour straight away when there is a connection; without one it waits for the next sync.</param>
    public TagsForm(
        Translations translations, LocalTagColourRepository? colours = null, TagColourSynchronizer? synchronizer = null)
    {
        _translations = translations;
        _colours = colours;
        _synchronizer = synchronizer;
    }

    /// <summary>The tags as the box holds them - "work, home".</summary>
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>What the owner's screen does when the reader is done with the box - press return, or tap a tag to add it.</summary>
    public ICommand? Save { get; set; }

    /// <summary>Tags this account already uses and this item has not got, to tap instead of typing.</summary>
    public ObservableCollection<string> Offered { get; } = [];

    /// <summary>One row per tag on the item, each with its palette.</summary>
    public ObservableCollection<TagColourRow> Rows { get; } = [];

    public bool HasOffered => Offered.Count > 0 && !IsReadOnly;

    public bool HasRows => Rows.Count > 0 && !IsReadOnly && _colours is not null;

    /// <summary>
    /// Said beside the palettes, because a colour is the one part of a private item's tags that is not
    /// sealed - see info/functionality.md, "Tags and their colours".
    /// </summary>
    public string ColourHint
        => _translations["A tag's colour is kept for your whole account, readable on the server - even for a tag only private notes and lists carry."];

    /// <summary>The words in the box, tidied as the server tidies them - see TagNames.</summary>
    public IReadOnlyList<string> Values => TagNames.Tidy(CategoryText.Split(Text));

    /// <summary>
    /// What a save sends: the words in the box - or null, "not said", for an item whose tags this phone never
    /// knew (LocalNote.Tags) and nobody touched. Sending an empty box for one of those would empty tags
    /// written in a browser before this phone learned about them.
    /// </summary>
    public IReadOnlyList<string>? ToSave => _wasKnown || _isTouched ? Values : null;

    /// <summary>Shows an item's tags - null for "not known" - with the ones this account already uses on offer.</summary>
    public async Task ShowAsync(
        IReadOnlyList<string>? tags, IEnumerable<string> known, CancellationToken cancellationToken = default)
    {
        _isShowing = true;
        _wasKnown = tags is not null;
        _isTouched = false;
        Text = CategoryText.Join(tags ?? []);
        _isShowing = false;

        _colourMap = _colours is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await _colours.ColoursAsync(cancellationToken);
        var coloured = _colours is null ? [] : await _colours.ColouredTagsAsync(cancellationToken);
        _known =
        [
            .. known.Concat(coloured)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
        ];
        Refresh();
    }

    /// <summary>Adds a tag already in use, and saves - tapping it is a decision, not something half-typed.</summary>
    [RelayCommand]
    private void Add(string? tag)
    {
        if (tag is not { Length: > 0 } || IsReadOnly)
        {
            return;
        }

        Text = CategoryText.Join(Values.Append(tag));
        if (Save?.CanExecute(null) is true)
        {
            Save.Execute(null);
        }
    }

    /// <summary>
    /// Gives a tag a colour for the whole account - on this phone now, offline included, and sent straight
    /// away when there is a connection. One that cannot be sent now waits for the next sync.
    /// </summary>
    [RelayCommand]
    private async Task ColourAsync(TagSwatch? swatch)
    {
        if (swatch is null || _colours is null || IsReadOnly)
        {
            return;
        }

        await _colours.SetAsync(swatch.Tag, swatch.Colour);
        _colourMap = await _colours.ColoursAsync();
        Refresh();

        if (_synchronizer is null)
        {
            return;
        }

        try
        {
            await _synchronizer.SynchroniseAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // No connection, or no sensible answer: the colour is kept here and marked to send, and the
            // next sync sends it. Nothing to say - the chip already shows it.
        }
    }

    partial void OnTextChanged(string value)
    {
        if (!_isShowing)
        {
            _isTouched = true;
        }

        Refresh();
    }

    partial void OnIsReadOnlyChanged(bool value) => Refresh();

    private void Refresh()
    {
        var values = Values;

        Offered.Clear();
        foreach (var tag in _known.Where(tag => !values.Contains(tag, StringComparer.CurrentCultureIgnoreCase)).Take(MostOffered))
        {
            Offered.Add(tag);
        }

        Rows.Clear();
        foreach (var tag in values)
        {
            var colour = _colourMap.TryGetValue(TagNames.KeyOf(tag), out var chosen) ? chosen : string.Empty;
            Rows.Add(new TagColourRow(tag, colour, SwatchesFor(tag, colour)));
        }

        OnPropertyChanged(nameof(HasOffered));
        OnPropertyChanged(nameof(HasRows));
    }

    private IReadOnlyList<TagSwatch> SwatchesFor(string tag, string chosen)
        =>
        [
            .. Palette.Select(colour => new TagSwatch(
                tag, colour, string.Equals(colour, chosen, StringComparison.OrdinalIgnoreCase),
                _translations.Format("Colour of {0}", tag))),
            // The way back to plain, beside the colours rather than hidden behind one of them.
            new TagSwatch(tag, string.Empty, chosen.Length == 0, _translations["No colour"])
        ];
}

/// <summary>One tag on the open item, as it is drawn now, with the colours it can be given.</summary>
public sealed record TagColourRow(string Tag, string Colour, IReadOnlyList<TagSwatch> Swatches);

/// <summary>One colour a tag can be given - an empty one taking its colour away - and whether it is the one it has.</summary>
public sealed record TagSwatch(string Tag, string Colour, bool IsChosen, string Description)
{
    /// <summary>The chosen one is ringed, so the palette says which it is without a word.</summary>
    public double EdgeThickness => IsChosen ? 3 : 1;
}
