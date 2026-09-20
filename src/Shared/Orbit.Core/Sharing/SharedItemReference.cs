namespace Orbit.Core.Sharing;

/// <summary>What a public link points at - a kind plus the item's own id, since each of these lives in a table of its own.</summary>
public sealed record SharedItemReference(SharedItemType ItemType, Guid ItemId);

/// <summary>The kinds of item a public link can be made for. Chat messages and locations are deliberately absent: both are only ever readable by their two parties.</summary>
public enum SharedItemType
{
    Note,
    TaskList,
    CalendarEvent,

    /// <summary>
    /// Keeps the old "warehouse" wording on purpose: the value is stored as text in OP_PUBLIC_SHARES
    /// and travels inside chat payloads delivered before the rename, so changing it would orphan every
    /// share link handed out so far. The type itself is Inventory everywhere else.
    /// </summary>
    Inventory,

    /// <summary>
    /// Somewhere on the map worth keeping - see Orbit.Core.Places.Place. Last rather than beside the
    /// others: the value is stored as text, but a client that reads it as a number would turn every
    /// stored "Inventory" into something else if one were inserted in the middle.
    /// </summary>
    Place,

    /// <summary>
    /// A whole folder, and through it everything filed under it - see Orbit.Core.Folders.Folder. Asked
    /// for on 2026-09-20: the two ways a single thing is handed on, applied to the tab it is under.
    ///
    /// The odd one out, and worth saying why. Every other value here names a thing somebody wrote; this
    /// one names a *place things are in*, so a link to it shows each of them in turn rather than one
    /// item (see PublicSharedItem.Items), and handing it on in chat is handing on each thing under it
    /// with its own grant. A folder itself is never shared - it stays the owner's own tab, and the
    /// recipient files what arrives wherever they like.
    ///
    /// Appended at the end, for the reason the comment above gives.
    /// </summary>
    Folder
}
