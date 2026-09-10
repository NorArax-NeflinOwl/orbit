namespace Orbit.Mobile.Data;

/// <summary>
/// A folder somebody made, as the phone holds it - one tab on the notes or the tasks screen. Mirrors
/// <see cref="Orbit.Contracts.Folders.FolderDto"/>, and that is all there is to it: a folder is a name
/// and a page, and what is *in* it is a field on the note or the list rather than a list kept here.
///
/// The three built-in folders (Public, Private, Finished) are not rows and never will be - which one
/// something is in is decided from what it already is. See <see cref="Orbit.Core.Folders.BuiltInFolder"/>
/// and <see cref="Orbit.Core.Folders.FolderPlacement"/>, which the phone and the browser share.
/// </summary>
public sealed class LocalFolder
{
    /// <summary>
    /// The key on this device, generated here and never changing - see <see cref="LocalNote.LocalId"/>,
    /// which is the same arrangement for the same reason. A note filed into a folder made offline
    /// points at *this*, so the id it points at does not have to be found and rewritten the moment the
    /// server names the folder something else.
    /// </summary>
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows it by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Which page it is a tab on - see Orbit.Core.Folders.FolderScope. "Notes" or "Tasks".</summary>
    public string Scope { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
