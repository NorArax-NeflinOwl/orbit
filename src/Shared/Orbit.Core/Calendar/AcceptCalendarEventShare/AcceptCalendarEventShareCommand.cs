using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.AcceptCalendarEventShare;

/// <summary>Returns false when shareId doesn't exist, wasn't offered to recipientUserId, or its source event is gone.</summary>
public sealed record AcceptCalendarEventShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
