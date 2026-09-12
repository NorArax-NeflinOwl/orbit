namespace Orbit.Core.Tasks;

/// <summary>
/// One way of getting an entry done - see <see cref="TaskItem.Alternatives"/>. Either a line of its own
/// ("buy a ready one"), ticked by hand, or another task list ("make it yourself"), which is done when
/// that list is.
///
/// A way that is a list keeps its words too, and they may be empty: the list's own title is then what
/// the reader sees. <see cref="IsDone"/> on such a way is never what a client says - it is worked out
/// from the list each time the entry is read (see <see cref="LinkedTaskCompletionResolver"/>), the same
/// rule a whole entry standing for lists has always followed.
/// </summary>
/// <param name="Description">What this way is, in the reader's words.</param>
/// <param name="LinkedTaskListId">The list this way is, or null for a line of its own.</param>
/// <param name="IsDone">Whether this way has been taken.</param>
public sealed record TaskItemAlternative(string Description, Guid? LinkedTaskListId = null, bool IsDone = false)
{
    /// <summary>Whether this way is another list rather than a line ticked by hand.</summary>
    public bool IsAList => LinkedTaskListId is not null;
}
