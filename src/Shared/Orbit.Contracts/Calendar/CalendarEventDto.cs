namespace Orbit.Contracts.Calendar;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content, so they
/// sit alongside Id rather than inside Details - see Orbit.Contracts.Notes.NoteDto's comment for what
/// each means and how the client uses OriginalOwnerUserId.
/// </summary>
/// <param name="IsSharedWithOthers">
/// The owner's side of sharing: somebody else holds accepted access. Always false when
/// <paramref name="IsShared"/> is true, since that describes the other end of the same relationship.
/// The mobile client needs it to decide what may be edited offline - it cannot hold an edit lock, so
/// anything another person can change is read-only until it is back online (info/orbit-maui-plan.md
/// §5.4). Mirrors NoteDto.
/// </param>
/// <param name="FolderId">
/// The folder its owner filed it under, or null for one filed nowhere - which is the Public tab rather
/// than no tab at all (Orbit.Core.Folders.BuiltInFolder). Always null for somebody reading this through
/// a share: a folder is where its owner keeps their own things, and the recipient files it wherever
/// they like on their own page. Defaulted and last, so an older client reads past it.
/// </param>
public sealed record CalendarEventDto(
    Guid Id, CalendarEventDetailsDto Details, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId,
    bool IsSharedWithOthers = false, Guid? FolderId = null,
    /// <summary>
    /// Whether its owner has put it away - see Orbit.Core.Folders.BuiltInFolder.Archived. False is what
    /// everything stored before the column existed is, and what a server that has not learned about
    /// archiving answers. Defaulted and last, so an older client reads past it.
    /// </summary>
    bool IsArchived = false);
