using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEventById;

public sealed record GetCalendarEventByIdQuery(Guid UserId, Guid Id) : IRequest<CalendarEvent?>;
