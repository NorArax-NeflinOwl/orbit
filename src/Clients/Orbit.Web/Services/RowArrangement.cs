namespace Orbit.Web.Services;

/// <summary>
/// Putting a row where somebody wants it, for the editors that let a list be arranged by hand - a task
/// list's items, an inventory's shelf, the task cards - because the order those are written in is the
/// order they are saved and read back in.
///
/// Two ways to the same move, because one of them needs a mouse. <see cref="DropOn"/> is dragging, and
/// browsers raise no drag events for a finger; <see cref="Move"/> is a step at a time, which a button
/// or a keyboard can ask for. Both end in the same list, reordered.
///
/// Rows are matched by reference: the editors hand back the very objects they are rendering, so a list
/// naming the same thing twice still moves the row that was picked up rather than the first match.
/// </summary>
public sealed class RowArrangement<TRow> where TRow : class
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

        return MoveTo(rows, dragged, rows.IndexOf(target));
    }

    /// <summary>
    /// Moves <paramref name="row"/> one place up (-1) or down (+1). Answers whether it actually moved:
    /// a row already at the end of the list stays where it is rather than wrapping round to the other,
    /// which is not what "down" means to anybody looking at the bottom row.
    /// </summary>
    public static bool Move(IList<TRow> rows, TRow row, int offset)
    {
        var from = rows.IndexOf(row);
        return from >= 0 && MoveTo(rows, row, from + offset);
    }

    /// <summary>Whether a move by <paramref name="offset"/> would land anywhere - what greys a button out.</summary>
    public static bool CanMove(IList<TRow> rows, TRow row, int offset)
    {
        var from = rows.IndexOf(row);
        return from >= 0 && from + offset >= 0 && from + offset < rows.Count;
    }

    private static bool MoveTo(IList<TRow> rows, TRow row, int onto)
    {
        var from = rows.IndexOf(row);
        if (from < 0 || onto < 0 || onto >= rows.Count || from == onto)
        {
            return false;
        }

        rows.RemoveAt(from);
        rows.Insert(onto, row);
        return true;
    }
}
