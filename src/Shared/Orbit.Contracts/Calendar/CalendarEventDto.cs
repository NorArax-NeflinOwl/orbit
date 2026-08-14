namespace Orbit.Contracts.Calendar;

public sealed record CalendarEventDto(Guid Id, CalendarEventDetailsDto Details, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
