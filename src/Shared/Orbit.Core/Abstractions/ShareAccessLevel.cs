namespace Orbit.Core.Abstractions;

/// <summary>
/// The level of access a share offer grants its recipient once accepted - shared by calendar event,
/// note, and task list sharing (see CalendarEventShare, NoteShare, TaskListShare) so all three use the
/// same two-value concept instead of three copies of it.
/// </summary>
public enum ShareAccessLevel
{
    /// <summary>The recipient can view their accepted copy but not change it - the default.</summary>
    ReadOnly,

    /// <summary>The recipient can also edit their accepted copy.</summary>
    CanEdit
}
