using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ShareCalendarEvent;

/// <summary>Returns null under the same conditions as Orbit.Core.Notes.ShareNote.ShareNoteCommand - see its comment.</summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareCalendarEventCommand(
    Guid OwnerUserId, Guid CalendarEventId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
