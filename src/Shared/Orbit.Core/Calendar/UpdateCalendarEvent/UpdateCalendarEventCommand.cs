using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.UpdateCalendarEvent;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateCalendarEventCommand(Guid UserId, Guid Id, CalendarEventDetails Details) : IRequest<EditOutcome>;
