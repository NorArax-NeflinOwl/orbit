using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

public sealed record CreateCalendarEventCommand(Guid UserId, CalendarEventDetails Details) : IRequest<Guid>;
