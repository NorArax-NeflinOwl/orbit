namespace Orbit.Contracts.Sync;

/// <summary>
/// What changed since a client last asked: the items it should write down, and the ids it should drop.
/// Both halves are needed - a deleted item is absent from the changed list, which is indistinguishable
/// from one the client already holds, so deletions have to be named (see Orbit.Core.Sync.SyncTombstone).
///
/// <paramref name="Cursor"/> is what to pass back as <c>since</c> next time, **verbatim**. It is a
/// string rather than a timestamp on purpose: it is the server's to interpret, not the client's to do
/// arithmetic on, and being written as ISO-8601 UTC ending in "Z" it survives being dropped straight
/// into a query string. A "+00:00" offset would not - "+" means a space there, and the server would
/// receive a time it cannot read.
///
/// The cursor is read from the server's clock before the query runs, and <c>since</c> is inclusive, so
/// a change landing while the request is in flight is sent again rather than falling into the gap
/// between two calls. Applying a change twice is harmless - the client writes what the server says -
/// whereas missing one leaves the two permanently out of step.
/// </summary>
public sealed record ChangeFeedDto<TItem>(IReadOnlyList<TItem> Changed, IReadOnlyList<Guid> DeletedIds, string Cursor);
