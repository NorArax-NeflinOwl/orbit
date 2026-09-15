namespace Orbit.Core.Notes;

/// <summary>
/// A table inside a note: rows of cells, each cell a stretch of words with the marks a line's words can
/// carry. Settled with the user on 2026-09-14 as <b>a kind of line</b> rather than a block of its own -
/// a note stays a list of <see cref="NoteContentLine"/>, and a line that carries one of these is the
/// table (see <see cref="NoteContentLine.Table"/>). That keeps every rule about a note's surface a rule
/// about lines, and it is the same answer a picture will want.
///
/// Always rectangular: every row has the same number of cells (<see cref="Columns"/>), and there is at
/// least one row and one column. <see cref="NoteTables"/> keeps it so; a table that would lose its last
/// row or column is not a table any more, and the line becomes ordinary writing instead.
/// </summary>
public sealed record NoteTable(IReadOnlyList<NoteTableRow> Rows)
{
    /// <summary>How many cells across, which every row has.</summary>
    public int Columns => Rows.Count == 0 ? 0 : Rows[0].Cells.Count;

    /// <summary>
    /// Two tables are the same table when every cell says the same thing - written out because a record
    /// compares a list by which list it is, and a note's lines are compared by what they say (see
    /// NoteContentLine.Equals).
    /// </summary>
    public bool Equals(NoteTable? other) => other is not null && Rows.SequenceEqual(other.Rows);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var row in Rows)
        {
            hash.Add(row);
        }

        return hash.ToHashCode();
    }
}

/// <summary>One row of a table, left to right.</summary>
public sealed record NoteTableRow(IReadOnlyList<NoteTableCell> Cells)
{
    public bool Equals(NoteTableRow? other) => other is not null && Cells.SequenceEqual(other.Cells);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var cell in Cells)
        {
            hash.Add(cell);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// One cell: its words and the marks on stretches of them - the same shape a line's words have, so bold
/// inside a cell is the bold everything else already draws. A cell has no style and no box: it is words
/// in a grid, not a line of the note.
/// </summary>
public sealed record NoteTableCell(string Text, IReadOnlyList<NoteTextRun>? Marks = null)
{
    /// <summary>An empty cell - what a new row or column is filled with.</summary>
    public static readonly NoteTableCell Empty = new(string.Empty);

    /// <summary>The marks as something to read without a null check - see <see cref="Marks"/>.</summary>
    public IReadOnlyList<NoteTextRun> AllMarks => Marks ?? NoteTextMarks.None;

    public bool Equals(NoteTableCell? other)
        => other is not null && Text == other.Text && AllMarks.SequenceEqual(other.AllMarks);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Text);
        foreach (var run in AllMarks)
        {
            hash.Add(run);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// What can be done to a table's shape. Each answers a new table rather than changing the one given,
/// as every edit on this surface does, and each keeps the table rectangular. Taking away the last row
/// or the last column answers <c>null</c>: there is no such thing as a table with nothing in it, and the
/// line it was on becomes ordinary writing - see NoteSurfaceEdits.
/// </summary>
public static class NoteTables
{
    /// <summary>What the table tool starts with - two by two, as Apple Notes starts one.</summary>
    public const int StartingRows = 2;

    public const int StartingColumns = 2;

    /// <summary>A table of empty cells, <paramref name="rows"/> down and <paramref name="columns"/> across - at least one of each.</summary>
    public static NoteTable Empty(int rows = StartingRows, int columns = StartingColumns)
    {
        rows = Math.Max(1, rows);
        columns = Math.Max(1, columns);
        return new NoteTable([.. Enumerable.Range(0, rows).Select(_ => EmptyRow(columns))]);
    }

    private static NoteTableRow EmptyRow(int columns)
        => new([.. Enumerable.Range(0, columns).Select(_ => NoteTableCell.Empty)]);

    /// <summary>
    /// The table made rectangular: every row as wide as the widest, padded with empty cells, and never
    /// fewer than one row of one cell. What a table read off the wire goes through, since nothing there
    /// promises the rows agree.
    /// </summary>
    public static NoteTable Squared(NoteTable table)
    {
        var columns = Math.Max(1, table.Rows.Select(row => row.Cells.Count).DefaultIfEmpty(0).Max());
        List<NoteTableRow> rows = table.Rows.Count == 0
            ? [EmptyRow(columns)]
            : table.Rows.Select(row => Padded(row, columns)).ToList();
        return new NoteTable(rows);
    }

    private static NoteTableRow Padded(NoteTableRow row, int columns)
        => row.Cells.Count == columns
            ? row
            : new NoteTableRow([.. row.Cells.Take(columns), .. Enumerable.Repeat(NoteTableCell.Empty, Math.Max(0, columns - row.Cells.Count))]);

    /// <summary>A new empty row under row <paramref name="afterRow"/> - or at the top, for -1.</summary>
    public static NoteTable WithRowAfter(NoteTable table, int afterRow)
    {
        var rows = table.Rows.ToList();
        rows.Insert(Math.Clamp(afterRow + 1, 0, rows.Count), EmptyRow(table.Columns));
        return new NoteTable(rows);
    }

    /// <summary>A new empty column to the right of column <paramref name="afterColumn"/> - or at the left, for -1.</summary>
    public static NoteTable WithColumnAfter(NoteTable table, int afterColumn)
    {
        var at = Math.Clamp(afterColumn + 1, 0, table.Columns);
        return new NoteTable([.. table.Rows.Select(row =>
        {
            var cells = row.Cells.ToList();
            cells.Insert(at, NoteTableCell.Empty);
            return new NoteTableRow(cells);
        })]);
    }

    /// <summary>The table without row <paramref name="row"/>, or null when that was its last row.</summary>
    public static NoteTable? WithoutRow(NoteTable table, int row)
    {
        if (row < 0 || row >= table.Rows.Count)
        {
            return table;
        }

        if (table.Rows.Count == 1)
        {
            return null;
        }

        var rows = table.Rows.ToList();
        rows.RemoveAt(row);
        return new NoteTable(rows);
    }

    /// <summary>The table without column <paramref name="column"/>, or null when that was its last column.</summary>
    public static NoteTable? WithoutColumn(NoteTable table, int column)
    {
        if (column < 0 || column >= table.Columns)
        {
            return table;
        }

        if (table.Columns == 1)
        {
            return null;
        }

        return new NoteTable([.. table.Rows.Select(row =>
        {
            var cells = row.Cells.ToList();
            cells.RemoveAt(column);
            return new NoteTableRow(cells);
        })]);
    }

    /// <summary>The table with one cell's words replaced. A place outside the table leaves it as it is.</summary>
    public static NoteTable WithCell(NoteTable table, int row, int column, NoteTableCell cell)
    {
        if (row < 0 || row >= table.Rows.Count || column < 0 || column >= table.Columns)
        {
            return table;
        }

        var cells = table.Rows[row].Cells.ToList();
        cells[column] = cell with { Marks = NoteTextMarks.Normalized(cell.AllMarks, cell.Text.Length) };
        var rows = table.Rows.ToList();
        rows[row] = new NoteTableRow(cells);
        return new NoteTable(rows);
    }

    /// <summary>Whether anything at all is written in it - what decides if taking it away is a loss.</summary>
    public static bool IsBlank(NoteTable table)
        => table.Rows.All(row => row.Cells.All(cell => cell.Text.Length == 0));
}
