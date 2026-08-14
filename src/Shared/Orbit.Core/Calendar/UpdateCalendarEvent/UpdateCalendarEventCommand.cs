using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.UpdateCalendarEvent;

public sealed record UpdateCalendarEventCommand(Guid UserId, Guid Id, CalendarEventDetails Details) : IRequest<bool>;
