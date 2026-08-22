using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ShareCalendarEvent;

/// <summary>Returns null instead of a share id when calendarEventId doesn't exist or isn't owned by ownerUserId.</summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareCalendarEventCommand(Guid OwnerUserId, Guid CalendarEventId, Guid RecipientUserId) : IRequest<Guid?>;
