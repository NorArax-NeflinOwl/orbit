namespace Orbit.Core.Tasks;

/// <summary>
/// What a task list is for. The kind decides what else a list carries: a calendar list is one whose
/// entries are places to be rather than things to fetch, so it also holds a location (see
/// <see cref="TaskList.Location"/>), while a checklist has nowhere to be and stores none.
///
/// Stored by name rather than by number (see TaskEntity.Kind), so this order can change without
/// touching a single stored row.
/// </summary>
public enum TaskListKind
{
    /// <summary>The ordinary list: things to do, ticked off one by one.</summary>
    Checklist,

    /// <summary>A list of appointments, which happen somewhere as well as at some time.</summary>
    Calendar
}
