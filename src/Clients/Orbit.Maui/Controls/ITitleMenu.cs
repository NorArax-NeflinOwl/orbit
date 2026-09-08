using System.Windows.Input;
using Orbit.Mobile.Screens;

namespace Orbit.Maui.Controls;

/// <summary>
/// A screen whose own menu hangs from its name in the top bar.
///
/// The design puts one dropdown under the title and gives it everything the screen as a whole can be
/// asked - how to sort it, what to leave out, what to do with the thing being looked at. That is the
/// menu Orbit used to keep in two places at once: a three-dot button in the page header for the
/// settings kind, and another in the bottom rail for the actions kind. The rail is gone and the header
/// no longer holds the title, so both come here.
///
/// What does *not* come here is a menu about one row - a card, a person, a message, a line of a note.
/// Those carry a parameter naming their subject and have nowhere else to live; they keep their own
/// three dots where the row is.
///
/// A page that does not implement this simply gets no chevron beside its name, which is how the screens
/// are converted one at a time without any of them looking half-finished.
/// </summary>
public interface ITitleMenu
{
    /// <summary>The page's one menu, which <c>MenuOverlay</c> draws - see ScreenMenu.</summary>
    ScreenMenu Menu { get; }

    /// <summary>Fills <see cref="Menu"/> and opens it. The bar calls this when the name is pressed.</summary>
    ICommand ShowTitleMenuCommand { get; }
}
