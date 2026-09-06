using Orbit.Core.Abstractions;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Sync;

/// <summary>
/// What the level something was shared at lets this phone do with it.
///
/// Separate from <see cref="OfflineEditPolicy"/>, which answers a different question: that one is about
/// whether an edit can be made safely without a connection, and says nothing at all while online. This
/// one holds whether or not there is a connection, because it is not about locks - it is about what the
/// owner allowed.
///
/// It was missing, and the shape of the gap is worth recording: the phone asked only the offline policy,
/// so anything shared read-only opened as an ordinary editable screen the moment the phone was online.
/// The edit was applied locally, queued, refused by the server with a 403, and eventually given up on -
/// so somebody's work disappeared some minutes after they did it, with no way to tell why. Orbit.Web
/// has disabled the whole form for a read-only share all along (see its SharedItemAccess).
///
/// The rules themselves are Orbit.Core's <see cref="ShareAccess"/> rather than string comparisons here:
/// the server decides them, and EditOnly permits editing too - a check written as "== CanEdit" quietly
/// calls an editor a reader.
/// </summary>
public static class SharedItemAccess
{
    /// <summary>
    /// Whether the share this arrived under permits changing it. True for the reader's own item, which
    /// arrived under no share at all.
    /// </summary>
    public static bool AllowsEditing(ISharedState item)
        => !item.IsShared || LevelOf(item).AllowsEditing();

    /// <summary>
    /// Whether it can be handed on to somebody else. True for the reader's own item; for one that
    /// arrived through a share, only where that share permits granting anything at all - a read-only
    /// recipient shares nothing. The same rule Orbit.Web's own SharedItemAccess.CanShare asks, and it
    /// asks Orbit.Core rather than comparing strings for the reason the class comment gives.
    /// </summary>
    public static bool AllowsSharing(ISharedState item)
        => !item.IsShared || LevelOf(item).CanGrant(ShareAccessLevel.ReadOnly);

    /// <summary>
    /// Why it cannot be changed, or empty when it can. One wording for all four kinds: what it says is
    /// about the share rather than about the thing, and the reader's own next step is the same either
    /// way - see SharePanel's "Ask to edit this", which is offered beside it.
    /// </summary>
    public static string WhyItCannotBeEdited(ISharedState item, Translations translations)
        => AllowsEditing(item)
            ? string.Empty
            : translations["Shared with you to read. Ask whoever shared it if you need to change it."];

    /// <summary>
    /// An unrecognised level reads as ReadOnly, as it does in the browser: a level this build does not
    /// know is one added after it, and the safe reading of that is the narrowest one.
    /// </summary>
    private static ShareAccessLevel LevelOf(ISharedState item)
        => Enum.TryParse<ShareAccessLevel>(item.AccessLevel, out var level) ? level : ShareAccessLevel.ReadOnly;
}
