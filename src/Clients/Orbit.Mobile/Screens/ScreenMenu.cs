using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Orbit.Mobile.Screens;

/// <summary>
/// The extra actions a screen has but does not want in its main row - Orbit.Web's OverflowMenu, which
/// is the same three-dot menu on a card, in a page header and on an editing screen's rail.
///
/// One of these per screen rather than one per card: only one menu is ever open, and the panel that
/// draws it has to sit above everything else on the page - inside a card it would be clipped by the
/// row it is in. Whoever opens it says what is in it, so the same panel serves a card's Edit/Share/
/// Delete and a header's "how am I reading this today".
/// </summary>
public sealed partial class ScreenMenu : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>What the menu is about, where its entries are settings rather than actions. Optional.</summary>
    [ObservableProperty]
    private string? _heading;

    /// <summary>
    /// Whether the panel hangs downwards from what opened it or upwards out of it. A menu opened from
    /// the bar along the foot of an editing screen would otherwise open into the ground - which is the
    /// rule app.css writes for .editor-rail .overflow-menu-dropdown at the 680px breakpoint.
    /// </summary>
    [ObservableProperty]
    private bool _opensUpwards;

    public ObservableCollection<ScreenMenuEntry> Entries { get; } = [];

    /// <summary>
    /// Opens the menu on a fresh set of entries. Everything about the menu is replaced, heading
    /// included, so a menu opened from somewhere else cannot show the last one's leftovers.
    /// </summary>
    public void Show(IEnumerable<ScreenMenuEntry> entries, string? heading = null, bool opensUpwards = false)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            entry.Menu = this;
            Entries.Add(entry);
        }

        Heading = heading;
        OpensUpwards = opensUpwards;
        IsOpen = Entries.Count > 0;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;
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
