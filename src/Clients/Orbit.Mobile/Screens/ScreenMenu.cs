using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Orbit.Mobile.Screens;

/// <summary>
/// The extra actions a screen has but does not want in its main row: the menu that hangs under the
/// screen's name, and the three-dot menu on a card, a person, a message or a line of a note.
///
/// One of these per screen rather than one per card: only one menu is ever open, and the panel that
/// draws it has to sit above everything else on the page - inside a card it would be clipped by the
/// row it is in. Whoever opens it says what is in it, so the same panel serves a card's Delete and the
/// title's "how am I reading this today".
/// </summary>
public sealed partial class ScreenMenu : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>What the menu is about, where its entries are settings rather than actions. Optional.</summary>
    [ObservableProperty]
    private string? _heading;

    /// <summary>Where the panel hangs from - see <see cref="MenuPlacement"/>.</summary>
    [ObservableProperty]
    private MenuPlacement _placement;

    public ObservableCollection<ScreenMenuEntry> Entries { get; } = [];

    /// <summary>
    /// Opens the menu on a fresh set of entries. Everything about the menu is replaced, heading
    /// included, so a menu opened from somewhere else cannot show the last one's leftovers.
    /// </summary>
    public void Show(
        IEnumerable<ScreenMenuEntry> entries,
        string? heading = null,
        MenuPlacement placement = MenuPlacement.UnderTheTitle)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            entry.Menu = this;
            Entries.Add(entry);
        }

        Heading = heading;
        Placement = placement;
        IsOpen = Entries.Count > 0;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;
}

/// <summary>
/// Where a menu's panel hangs from. The browser positions a panel against the control that opened it
/// and clamps it to the window; on Android a panel drawn inside a card is clipped by the row it sits
/// in, so there is one panel per screen and it is told which edge to take.
/// </summary>
public enum MenuPlacement
{
    /// <summary>Centred under the screen's name in the top bar, which is where a screen's own menu lives.</summary>
    UnderTheTitle,

    /// <summary>
    /// Upwards out of the foot of the screen. What a menu opened from a row low down the page has to
    /// do: hanging downwards from the row it belongs to would open into the ground.
    /// </summary>
    FromTheFoot
}

/// <summary>
/// One entry - Orbit.Web's .avatar-dropdown-item. A label, whether it is the chosen one, whether it
/// can be pressed at all, and what pressing it does.
/// </summary>
public sealed partial class ScreenMenuEntry : ObservableObject
{
    private readonly Action _chosen;

    public ScreenMenuEntry(string label, Action chosen, bool isChosen = false, bool canBeChosen = true, bool staysOpen = false)
    {
        Label = label;
        _chosen = chosen;
        IsChosen = isChosen;
        CanBeChosen = canBeChosen;
        StaysOpen = staysOpen;
    }

    public string Label { get; }

    /// <summary>
    /// The one currently in force, in a menu of settings. Drawn with a tick in the column the web
    /// keeps for it, so the entries still line up where none of them is chosen.
    /// </summary>
    [ObservableProperty]
    private bool _isChosen;

    /// <summary>
    /// An entry that cannot do anything yet says so by looking spent rather than by vanishing: the
    /// reader learns the option exists and what it is waiting for.
    /// </summary>
    public bool CanBeChosen { get; }

    /// <summary>
    /// For an entry that is a setting rather than an action. Closing after each one would make
    /// changing two a chore - which is the exception Orbit.Web's OverflowMenu.StaysOpen makes.
    /// </summary>
    public bool StaysOpen { get; }

    /// <summary>The tick's column, empty for every entry that is not the chosen one.</summary>
    public string Mark => IsChosen ? "✓" : string.Empty;

    internal ScreenMenu? Menu { get; set; }

    [RelayCommand]
    private void Choose()
    {
        if (!CanBeChosen)
        {
            return;
        }

        if (!StaysOpen && Menu is not null)
        {
            Menu.IsOpen = false;
        }

        _chosen();
    }

    partial void OnIsChosenChanged(bool value) => OnPropertyChanged(nameof(Mark));
}
