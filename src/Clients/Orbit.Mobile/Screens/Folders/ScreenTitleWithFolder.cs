using Orbit.Core.Folders;

namespace Orbit.Mobile.Screens.Folders;

/// <summary>
/// What the bar calls a list screen: its own name, and the folder it is being read under where that is
/// not the ordinary one.
///
/// The browser has the folders as a row of tabs above the cards, so what is chosen is on the screen
/// whether anybody looks for it or not. The phone has them in the menu under the screen's name, which
/// is shut - so a screen narrowed to "Work" looked exactly like one showing everything, and the only
/// way to find out was to open the menu. Asked for on 2026-09-24.
///
/// Public is left unsaid, as the same request asks: it is where a page opens and where anything unfiled
/// already is, so naming it would put a word on every screen to say "nothing in particular".
/// </summary>
public static class ScreenTitleWithFolder
{
    /// <param name="screen">The screen's own name - "Notes", or the calendar's month.</param>
    /// <param name="folderName">
    /// What the chosen folder is called, as the menu draws it: a built-in one's translated word or the
    /// name somebody gave. Empty is treated as nothing to add, which is what a screen whose folders
    /// have not been read yet says.
    /// </param>
    public static string Of(string screen, FolderKey chosen, string folderName)
        => chosen == FolderKey.Default || folderName.Trim().Length == 0
            ? screen
            : $"{screen} · {folderName.Trim()}";
}
