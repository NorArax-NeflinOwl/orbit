namespace Orbit.Contracts.Calendar;

/// <summary>IsShared/SharedByUserName describe provenance, not content, so they sit alongside Id rather than inside Details.</summary>
public sealed record CalendarEventDto(
    Guid Id, CalendarEventDetailsDto Details, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName);
