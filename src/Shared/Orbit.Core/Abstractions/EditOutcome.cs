namespace Orbit.Core.Abstractions;

public enum EditOutcomeKind
{
    /// <summary>The resource doesn't exist, isn't accessible to the caller, or the caller's access level doesn't allow this edit - the same "not found" response either way, so a caller can't distinguish the cases by probing ids.</summary>
    NotFound,

    /// <summary>Someone else currently holds the edit lock - see LockedByUserName.</summary>
    Locked,

    Success
}

/// <summary>
/// Result of an UpdateNoteCommand/UpdateTaskListCommand/UpdateCalendarEventCommand (or an
/// AcquireXLockCommand) attempt - shared across all three domains and both kinds of command since the
/// three possible outcomes, and what a caller needs to know about each, are identical.
/// </summary>
public sealed record EditOutcome(EditOutcomeKind Kind, string? LockedByUserName = null)
{
    public static readonly EditOutcome NotFound = new(EditOutcomeKind.NotFound);
    public static readonly EditOutcome Success = new(EditOutcomeKind.Success);
    public static EditOutcome LockedBy(string userName) => new(EditOutcomeKind.Locked, userName);
}
