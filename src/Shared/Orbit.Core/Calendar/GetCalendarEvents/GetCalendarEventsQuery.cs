using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.GetCalendarEvents;

public sealed record GetCalendarEventsQuery(Guid UserId) : IRequest<IReadOnlyList<CalendarEvent>>;
