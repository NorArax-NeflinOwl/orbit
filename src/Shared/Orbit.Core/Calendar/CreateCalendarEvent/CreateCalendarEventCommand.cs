using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.CreateCalendarEvent;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateCalendarEventCommand(Guid UserId, CalendarEventDetails Details) : IRequest<Guid>;
