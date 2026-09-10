using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// What the three folders nobody made are called, in the reader's language.
///
/// Its own class because two things now ask: the row of tabs, and the button a phone folds that row into
/// - which says which folder is open rather than the word "Menu". Two copies of this switch would be two
/// chances for the tab and the button above it to disagree about the name of the same folder.
/// </summary>
public static class BuiltInFolderNames
{
    public static string Of(BuiltInFolder builtIn, Translations translations) => builtIn switch
    {
        BuiltInFolder.Private => translations["Private"],
        BuiltInFolder.Finished => translations["Finished"],
        _ => translations["Public"]
    };

    /// <summary>The name of whichever folder is open on this page - a built-in one, or one somebody made.</summary>
    public static string Open(FolderState folders, FolderPage page, Translations translations)
        => folders.NameOf(folders.ChosenOn(page), builtIn => Of(builtIn, translations));
}
