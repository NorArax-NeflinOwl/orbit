namespace Orbit.Contracts.Sharing;

/// <summary>
/// 409 response body for an update or lock-acquire attempt on a note/task list/calendar event someone
/// else is currently editing - see Orbit.Core.Abstractions.EditOutcome.
/// </summary>
public sealed record LockConflictDto(string LockedByUserName);
