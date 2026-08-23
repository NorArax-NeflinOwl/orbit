using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.AcquireCalendarEventLock;

/// <summary>Mirrors Orbit.Core.Notes.AcquireNoteLock.AcquireNoteLockCommand - see its comment.</summary>
public sealed record AcquireCalendarEventLockCommand(Guid UserId, Guid CalendarEventId) : IRequest<EditOutcome>;
