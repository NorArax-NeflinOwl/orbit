namespace Orbit.Contracts.Notes;

/// <summary>
/// What changed since a client last asked: the notes it should write down, and the ids it should drop.
/// Both halves are needed - a deleted note is absent from the changed list, which is indistinguishable
/// from one the client already holds, so deletions have to be named (see Orbit.Core.Sync.SyncTombstone).
///
/// <paramref name="AsOfUtc"/> is the cursor for the next call, taken from the server's clock before the
/// query ran. Pass it back as <c>since</c>, which is **inclusive**: a change sitting exactly on the
/// boundary is sent again rather than risking it falling between two calls. Applying a change twice is
/// harmless - the client writes what the server says - whereas missing one leaves the two permanently
/// out of step.
///
/// It serialises with a "+00:00" offset, and "+" means a space in a query string, so a client that
/// pastes the value straight into the next URL sends a timestamp the server cannot read. Escape it.
/// </summary>
public sealed record NoteChangesDto(
    IReadOnlyList<NoteDto> Changed, IReadOnlyList<Guid> DeletedIds, DateTimeOffset AsOfUtc);
