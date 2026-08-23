namespace Orbit.Contracts.Calendar;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content, so they
/// sit alongside Id rather than inside Details - see Orbit.Contracts.Notes.NoteDto's comment for what
/// each means and how the client uses OriginalOwnerUserId.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id, CalendarEventDetailsDto Details, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId);
