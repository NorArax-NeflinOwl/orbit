namespace Orbit.Web.Services;

/// <summary>
/// One row being dragged to a new place in a list, and the move itself. Held by the editors that let
/// somebody arrange what they are editing - a task list's items, a warehouse's shelf - because the
/// order those are written in is the order they are saved and read back in.
///
/// Rows are matched by reference: the editors hand back the very objects they are rendering, so a list
/// naming the same thing twice still moves the row that was picked up rather than the first match.
/// </summary>
public sealed class DragReordering<TRow> where TRow : class
{
    /// <summary>What is being dragged right now, or null when nothing is.</summary>
    private TRow? _dragged;

    public bool IsDragging(TRow row) => ReferenceEquals(_dragged, row);

    public void Start(TRow row) => _dragged = row;

    public void Finish() => _dragged = null;

    /// <summary>
    /// Drops what is being dragged onto <paramref name="target"/>'s place, pushing the rest along.
    /// Answers whether anything actually moved, so a page only redraws when it did.
    /// </summary>
    public bool DropOn(IList<TRow> rows, TRow target)
    {
        if (_dragged is not { } dragged || ReferenceEquals(dragged, target))
        {
            return false;
        }

        var from = rows.IndexOf(dragged);
        var onto = rows.IndexOf(target);
        if (from < 0 || onto < 0)
        {
            return false;
        }

        rows.RemoveAt(from);
        rows.Insert(onto, dragged);
        return true;
    }
}
