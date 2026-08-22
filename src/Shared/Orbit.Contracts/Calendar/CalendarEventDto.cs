namespace Orbit.Contracts.Calendar;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel describe provenance, not content, so they sit alongside Id
/// rather than inside Details. AccessLevel is "ReadOnly" or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel)
/// and is only meaningful when IsShared is true.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id, CalendarEventDetailsDto Details, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel);
