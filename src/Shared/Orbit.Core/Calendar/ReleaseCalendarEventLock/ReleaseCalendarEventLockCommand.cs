using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar.ReleaseCalendarEventLock;

/// <summary>Mirrors Orbit.Core.Notes.ReleaseNoteLock.ReleaseNoteLockCommand - see its comment.</summary>
public sealed record ReleaseCalendarEventLockCommand(Guid UserId, Guid CalendarEventId) : IRequest<bool>;
