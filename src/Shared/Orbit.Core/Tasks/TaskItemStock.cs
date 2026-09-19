namespace Orbit.Core.Tasks;

/// <summary>
/// How a product entry stands with the shelf behind it. Three states rather than a flag, because the
/// two ways an entry comes to be crossed off mean opposite things to the shelf and both have to be told
/// apart later:
///
/// <list type="bullet">
///   <item><b>None</b> - nothing of this entry is on the shelf and nothing about it was decided there.
///     Where every entry starts.</item>
///   <item><b>Stocked</b> - somebody ticked it, and its own minimum went onto the shelf because of that
///     (Orbit.Core.Inventories.StockedEntryStock). Unticking it takes the same amount back off.</item>
///   <item><b>CrossedOffByTheShelf</b> - nobody ticked it: the shelf already held what it asked for, so
///     it was crossed off on the shelf's word (Orbit.Core.Inventories.StockedEntryCompletion). Nothing
///     of it is on the shelf, so there is nothing to take back - and if the shelf later stops holding
///     enough, this is the one that may be reopened again.</item>
/// </list>
///
/// Bookkeeping of the server's own: no client sends it, and none is told it. It exists so that the
/// arithmetic between a list and a shelf can be run again on any save without counting anything twice -
/// see StockedEntryStock, which is where the whole rule is written out.
///
/// Stored by name, like every other enum here, so a state added later reads as None on an older build
/// rather than as whichever number it happens to share.
/// </summary>
public enum TaskItemStock
{
    None,
    Stocked,
    CrossedOffByTheShelf
}
