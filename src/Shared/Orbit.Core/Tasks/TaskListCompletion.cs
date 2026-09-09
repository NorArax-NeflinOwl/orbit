namespace Orbit.Core.Tasks;

/// <summary>
/// What the reader has said about whether a list is finished, which is a different question from what
/// its entries say. Three answers, not two: the box on the list's form used to be a plain yes/no, so
/// there was no way to disagree with a list whose entries were all ticked off - unticking the box handed
/// the question straight back to the entries, which then said "finished" again and put the tick back.
///
/// <list type="bullet">
///   <item><b>FromTheEntries</b> - nobody has said, so the entries decide. Where every list starts, and
///     where it stays unless somebody presses the box.</item>
///   <item><b>Finished</b> - the reader says so, whatever is still on it. It survives an edit: adding an
///     entry to a list somebody closed does not quietly reopen it, because they said the <em>list</em>
///     was done rather than that its entries were.</item>
///   <item><b>Unfinished</b> - the reader says it is not, with every entry ticked off. The ordinary case
///     the old box could not say: the work is all done and the list itself is not - something is still
///     to be checked, handed over, paid for. The list reads as
///     <see cref="TaskListStatus.Incomplete"/> then, which is a status of its own so that "everything
///     is ticked and it is still open" is legible rather than looking like a list nobody started.</item>
/// </list>
/// </summary>
public enum TaskListCompletion
{
    FromTheEntries,
    Finished,
    Unfinished
}
