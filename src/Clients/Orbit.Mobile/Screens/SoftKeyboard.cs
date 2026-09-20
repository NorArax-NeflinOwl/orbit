using CommunityToolkit.Mvvm.ComponentModel;

namespace Orbit.Mobile.Screens;

/// <summary>
/// Whether the phone's keyboard is up, for the parts of a screen that should stand aside while somebody
/// is typing.
///
/// One shared answer rather than a question each screen asks: the keyboard belongs to the window, not to
/// a page, and the platform can only sensibly report it in one place (see MainActivity's insets
/// listener on Android). Here rather than in Orbit.Maui so a screen can be given one in a test without
/// a window to open a keyboard on.
///
/// What it is for so far is the advertising bar, which sits across the foot of every screen and is
/// exactly where a keyboard opens - so writing anything on a phone meant reading half a form with an
/// advert over the rest of it (reported 2026-09-20).
/// </summary>
public sealed partial class SoftKeyboard : ObservableObject
{
    [ObservableProperty]
    private bool _isUp;

    /// <summary>The other way round, for a control that is drawn while the keyboard is away.</summary>
    public bool IsAway => !IsUp;

    partial void OnIsUpChanged(bool value) => OnPropertyChanged(nameof(IsAway));
}
