namespace Orbit.Contracts.Notes;

/// <summary>One line of a note's content - either plain text, or a checklist item with its checked state.</summary>
/// <param name="IsFailed">
/// Crossed out rather than ticked - see Orbit.Core.Notes.NoteContentLine.IsFailed. Defaulted and last:
/// a client written before the cross existed sends nothing here, which reads as a line nobody crossed
/// out, and one reading a crossed-out line without knowing the field sees a line still to do.
/// </param>
/// <param name="Style">
/// What the line is - "Body", "Title", "Heading", "Subheading", "Monospaced", "Bulleted", "Dashed" or
/// "Numbered". See Orbit.Core.Notes.NoteLineStyle. Sent as the word rather than the number, the way every
/// other enum on this wire is, so a build that does not know a style can say which one it did not know.
///
/// Defaulted and last for the reason IsFailed is: a client written before styles existed sends nothing
/// here and its lines read as Body, which is what they were. A word this build does not know reads as
/// Body too - a line drawn plainly is a line, where a refused save would lose the writing.
/// </param>
/// <param name="Marks">
/// The marks on stretches of words inside the line - see <see cref="NoteTextRunDto"/>. Null for a line
/// with none, and for every line a client written before marks existed sends.
/// </param>
/// <param name="Table">
/// The table this line is, when it is one - see <see cref="NoteTableDto"/>. A line that carries a
/// table is the table: its text is empty and it has no box. Null for ordinary writing, and for every
/// line a client written before tables existed sends.
/// </param>
public sealed record NoteContentLineDto(
    string Text, bool IsChecklistItem, bool IsChecked, bool IsFailed = false, string Style = "Body",
    IReadOnlyList<NoteTextRunDto>? Marks = null, NoteTableDto? Table = null)
{
    /// <summary>The marks as something to read without a null check - see <see cref="Marks"/>.</summary>
    public IReadOnlyList<NoteTextRunDto> AllMarks => Marks ?? [];
}

/// <summary>A table inside a note - see Orbit.Core.Notes.NoteTable. Rows of cells; the server squares it up.</summary>
public sealed record NoteTableDto(IReadOnlyList<NoteTableRowDto> Rows);

public sealed record NoteTableRowDto(IReadOnlyList<NoteTableCellDto> Cells);

/// <summary>One cell: its words and the marks on stretches of them, the same shape a line's words travel in.</summary>
public sealed record NoteTableCellDto(string Text, IReadOnlyList<NoteTextRunDto>? Marks = null)
{
    public IReadOnlyList<NoteTextRunDto> AllMarks => Marks ?? [];
}

/// <summary>
/// One mark over one stretch of a line's characters - see Orbit.Core.Notes.NoteTextRun, which is this
/// with the mark as an enum rather than a word.
/// </summary>
/// <param name="Mark">
/// "Bold", "Italic", "Underlined" or "StruckThrough". The word rather than the number, as every other
/// enum on this wire; one this build does not know draws nothing rather than refusing the line.
/// </param>
public sealed record NoteTextRunDto(int Start, int Length, string Mark);
