namespace Orbit.Core.Tasks;

/// <summary>
/// Where a task list has got to. Derived from its items rather than stored, exactly like
/// TaskList.IsCompleted: a stored status is one more thing that can disagree with the checkboxes it
/// claims to describe, and every one of these answers is already sitting in the items - or, for
/// <see cref="Incomplete"/>, in the one answer the items cannot give (see TaskListCompletion).
/// </summary>
public enum TaskListStatus
{
    /// <summary>Nothing ticked off yet - including an empty list, which has nothing to tick.</summary>
    New,

    /// <summary>Started but not finished.</summary>
    Pending,

    /// <summary>Every item ticked off.</summary>
    Completed,

    /// <summary>
    /// Something on it is past its due date and still not done. Outranks New and Pending, since a list
    /// that is late is late whether or not it has been started - but never Completed, which has nothing
    /// left to be late.
    /// </summary>
    Overdue,

    /// <summary>
    /// The only thing still owed on it is a chore that comes round every day, and today's round has not
    /// been done - see <see cref="TaskItem.RemindDaily"/>.
    ///
    /// Its own status because <see cref="Overdue"/> was a lie about it. A daily chore keeps one due date
    /// that never moves, so the moment it passes the list reads "late" for as long as the chore exists -
    /// and it is not late, it is due again, which is what a daily chore is *for*. A restock round is the
    /// one every account has (see RestockTaskNaming), so this was the first thing a shelf did to a page.
    ///
    /// Below <see cref="Overdue"/>: a list carrying a missed deadline and a daily chore is late, and the
    /// deadline is the one worth saying. Above Pending and New for the same reason Overdue is - there is
    /// something waiting today.
    /// </summary>
    DueAgain,

    /// <summary>
    /// Every item ticked off and the list still open, because its owner said so - see
    /// TaskListCompletion.Unfinished. Its own status rather than Pending: the work really is all done,
    /// and a list reading "in progress" over a column of ticks describes neither of the two true things
    /// about it.
    /// </summary>
    Incomplete
}
