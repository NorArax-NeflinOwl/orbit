using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.DeleteCalendarEvent;

public sealed record DeleteCalendarEventCommand(Guid UserId, Guid Id) : IRequest<bool>;
