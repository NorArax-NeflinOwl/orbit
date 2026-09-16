using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Core.Notes;

namespace Orbit.Mobile.Screens.Notes;

/// <summary>
/// One cell of a table on the note screen, as the field it is written in. The words are corrected in
/// place, as a line's are (see <see cref="NoteLineRow"/>), and every change goes back to the line the
/// table is on, which puts it into the table (NoteTables.WithCell) and tells the view model - a cell is
/// not a point on the surface Orbit.Core decides edits on, so the field is the only way in.
///
/// Row and column are where it stands in the table it was built from. The line rebuilds its grid when
/// the table changes shape and only rewrites the words when it does not, so neither is ever stale while
/// the field is on the screen.
/// </summary>
public sealed partial class NoteTableCellField : ObservableObject
{
    public NoteTableCellField(NoteLineRow line, int row, int column, NoteTableCell cell)
    {
        Line = line;
        Row = row;
        Column = column;
        _text = cell.Text;
        Marks = cell.AllMarks;
    }

    /// <summary>The line whose table this cell is in - what a press on the table's menu acts on.</summary>
    public NoteLineRow Line { get; }

    public int Row { get; }

    public int Column { get; }

    [ObservableProperty]
    private string _text;

    /// <summary>
    /// The marks on stretches of the cell's words, carried and moved along with what is typed but not
    /// drawn: the field renders one face for the whole of it, as a line's field does.
    /// </summary>
    public IReadOnlyList<NoteTextRun> Marks { get; internal set; }

    /// <summary>What the cell said before its last change - see <see cref="NoteLineRow.TextBefore"/>.</summary>
    public string TextBefore { get; private set; } = string.Empty;

    partial void OnTextChanged(string? oldValue, string newValue) => TextBefore = oldValue ?? string.Empty;

    public NoteTableCell ToCell() => new(Text, Marks);
}

/// <summary>What was written in a cell: which cell, and what one change to its words did.</summary>
public sealed record NoteCellChange(NoteTableCellField Cell, NoteTextChange Change);
